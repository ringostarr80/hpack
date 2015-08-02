/*
 * Copyright 2015 Ringo Leese
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.IO;

namespace hpack
{
	public class Encoder
	{
		private static int BUCKET_SIZE = 17;
		private static byte[] EMPTY = { };

		// for testing
		private bool useIndexing;
		private bool forceHuffmanOn;
		private bool forceHuffmanOff;

		// a linked hash map of header fields
		private HeaderEntry[] headerFields = new HeaderEntry[BUCKET_SIZE];
		private HeaderEntry head = new HeaderEntry(-1, EMPTY, EMPTY, int.MaxValue, null);
		private int size;
		private int capacity;

		/**
		 * Creates a new encoder.
		 */
		public Encoder(int maxHeaderTableSize)
		{
			this.Init(maxHeaderTableSize, true, false, false);
		}

		/**
		 * Constructor for testing only.
		 */
		public Encoder(int maxHeaderTableSize, bool useIndexing, bool forceHuffmanOn, bool forceHuffmanOff)
		{
			this.Init(maxHeaderTableSize, useIndexing, forceHuffmanOn, forceHuffmanOff);
		}

		private void Init(int maxHeaderTableSize, bool useIndexing, bool forceHuffmanOn, bool forceHuffmanOff)
		{
			if (maxHeaderTableSize < 0) {
				throw new ArgumentException("Illegal Capacity: " + maxHeaderTableSize);
			}
			this.useIndexing = useIndexing;
			this.forceHuffmanOn = forceHuffmanOn;
			this.forceHuffmanOff = forceHuffmanOff;
			this.capacity = maxHeaderTableSize;
			head.Before = head.After = head;
		}

		/**
		 * Encode the header field into the header block.
		 */
		public void EncodeHeader(BinaryWriter output, byte[] name, byte[] value, bool sensitive)
		{
			// If the header value is sensitive then it must never be indexed
			if (sensitive) {
				int nameIndex = this.GetNameIndex(name);
				this.EncodeLiteral(output, name, value, HpackUtil.IndexType.NEVER, nameIndex);
				return;
			}

			// If the peer will only use the static table
			if (this.capacity == 0) {
				int staticTableIndex = StaticTable.GetIndex(name, value);
				if (staticTableIndex == -1) {
					int nameIndex = StaticTable.GetIndex(name);
					this.EncodeLiteral(output, name, value, HpackUtil.IndexType.NONE, nameIndex);
				} else {
					Encoder.EncodeInteger(output, 0x80, 7, staticTableIndex);
				}
				return;
			}

			int headerSize = HeaderField.SizeOf(name, value);

			// If the headerSize is greater than the max table size then it must be encoded literally
			if (headerSize > this.capacity) {
				int nameIndex = this.GetNameIndex(name);
				this.EncodeLiteral(output, name, value, HpackUtil.IndexType.NONE, nameIndex);
				return;
			}

			HeaderEntry headerField = this.GetEntry(name, value);
			if (headerField != null) {
				int index = this.GetIndex(headerField.Index) + StaticTable.Length;
				// Section 6.1. Indexed Header Field Representation
				Encoder.EncodeInteger(output, 0x80, 7, index);
			} else {
				int staticTableIndex = StaticTable.GetIndex(name, value);
				if (staticTableIndex != -1) {
					// Section 6.1. Indexed Header Field Representation
					Encoder.EncodeInteger(output, 0x80, 7, staticTableIndex);
				} else {
					int nameIndex = this.GetNameIndex(name);
					if (useIndexing) {
						this.EnsureCapacity(headerSize);
					}
					HpackUtil.IndexType indexType = useIndexing ? HpackUtil.IndexType.INCREMENTAL : HpackUtil.IndexType.NONE;
					this.EncodeLiteral(output, name, value, indexType, nameIndex);
					if (useIndexing) {
						this.Add(name, value);
					}
				}
			}
		}

		/**
		 * Set the maximum table size.
		 */
		public void SetMaxHeaderTableSize(BinaryWriter output, int maxHeaderTableSize)
		{
			if (maxHeaderTableSize < 0) {
				throw new ArgumentException("Illegal Capacity: " + maxHeaderTableSize);
			}
			if (this.capacity == maxHeaderTableSize) {
				return;
			}
			this.capacity = maxHeaderTableSize;
			this.EnsureCapacity(0);
			Encoder.EncodeInteger(output, 0x20, 5, maxHeaderTableSize);
		}

		/**
		 * Return the maximum table size.
		 */
		public int GetMaxHeaderTableSize()
		{
			return this.capacity;
		}

		/**
		 * Encode integer according to Section 5.1.
		 */
		private static void EncodeInteger(BinaryWriter output, int mask, int n, int i)
		{
			if (n < 0 || n > 8) {
				throw new ArgumentException("N: " + n);
			}
			int nbits = 0xFF >> (8 - n);
			if (i < nbits) {
				output.Write((byte)(mask | i));
			} else {
				output.Write((byte)(mask | nbits));
				int length = i - nbits;
				while(true) {
					if ((length & ~0x7F) == 0) {
						output.Write((byte)length);
						return;
					} else {
						output.Write((byte)((length & 0x7F) | 0x80));
						length >>= 7;
					}
				}
			}
		}

		/**
		 * Encode string literal according to Section 5.2.
		 */
		private void EncodeStringLiteral(BinaryWriter output, byte[] stringLiteral)
		{
			int huffmanLength = Huffman.ENCODER.GetEncodedLength(stringLiteral);
			if ((huffmanLength < stringLiteral.Length && !forceHuffmanOff) || forceHuffmanOn) {
				Encoder.EncodeInteger(output, 0x80, 7, huffmanLength);
				Huffman.ENCODER.Encode(output, stringLiteral);
			} else {
				Encoder.EncodeInteger(output, 0x00, 7, stringLiteral.Length);
				output.Write(stringLiteral, 0, stringLiteral.Length);
			}
		}

		/**
		 * Encode literal header field according to Section 6.2.
		 */
		private void EncodeLiteral(BinaryWriter output, byte[] name, byte[] value, HpackUtil.IndexType indexType, int nameIndex)
		{
			int mask;
			int prefixBits;
			switch(indexType) {
				case HpackUtil.IndexType.INCREMENTAL:
					mask = 0x40;
					prefixBits = 6;
					break;

				case HpackUtil.IndexType.NONE:
					mask = 0x00;
					prefixBits = 4;
					break;

				case HpackUtil.IndexType.NEVER:
					mask = 0x10;
					prefixBits = 4;
					break;

				default:
					throw new Exception("should not reach here");
			}
			Encoder.EncodeInteger(output, mask, prefixBits, nameIndex == -1 ? 0 : nameIndex);
			if (nameIndex == -1) {
				this.EncodeStringLiteral(output, name);
			}
			this.EncodeStringLiteral(output, value);
		}

		private int GetNameIndex(byte[] name)
		{
			int index = StaticTable.GetIndex(name);
			if (index == -1) {
				index = this.GetIndex(name);
				if (index >= 0) {
					index += StaticTable.Length;
				}
			}
			return index;
		}

		/**
		 * Ensure that the dynamic table has enough room to hold 'headerSize' more bytes.
		 * Removes the oldest entry from the dynamic table until sufficient space is available.
		 */
		private void EnsureCapacity(int headerSize)
		{
			while(this.size + headerSize > this.capacity) {
				int index = this.Length();
				if (index == 0) {
					break;
				}
				this.Remove();
			}
		}

		/**
		 * Return the number of header fields in the dynamic table.
		 * Exposed for testing.
		 */
		int Length()
		{
			return this.size == 0 ? 0 : this.head.After.Index - this.head.Before.Index + 1;
		}

		/**
		 * Return the size of the dynamic table.
		 * Exposed for testing.
		 */
		int GetSize()
		{
			return this.size;
		}

		/**
		 * Return the header field at the given index.
		 * Exposed for testing.
		 */
		HeaderField GetHeaderField(int index)
		{
			HeaderEntry entry = head;
			while(index-- >= 0) {
				entry = entry.Before;
			}
			return entry;
		}

		/**
		 * Returns the header entry with the lowest index value for the header field.
		 * Returns null if header field is not in the dynamic table.
		 */
		private HeaderEntry GetEntry(byte[] name, byte[] value)
		{
			if (this.Length() == 0 || name == null || value == null) {
				return null;
			}
			int h = Encoder.Hash(name);
			int i = Encoder.Index(h);
			for(HeaderEntry e = headerFields[i]; e != null; e = e.Next) {
				if (e.Hash == h && HpackUtil.Equals(name, e.Name) && HpackUtil.Equals(value, e.Value)) {
					return e;
				}
			}
			return null;
		}

		/**
		 * Returns the lowest index value for the header field name in the dynamic table.
		 * Returns -1 if the header field name is not in the dynamic table.
		 */
		private int GetIndex(byte[] name)
		{
			if (this.Length() == 0 || name == null) {
				return -1;
			}
			int h = Encoder.Hash(name);
			int i = Encoder.Index(h);
			int index = -1;
			for(HeaderEntry e = headerFields[i]; e != null; e = e.Next) {
				if (e.Hash == h && HpackUtil.Equals(name, e.Name)) {
					index = e.Index;
					break;
				}
			}
			return this.GetIndex(index);
		}

		/**
		 * Compute the index into the dynamic table given the index in the header entry.
		 */
		private int GetIndex(int index)
		{
			if (index == -1) {
				return index;
			}
			return index - head.Before.Index + 1;
		}

		/**
		 * Add the header field to the dynamic table.
		 * Entries are evicted from the dynamic table until the size of the table
		 * and the new header field is less than the table's capacity.
		 * If the size of the new entry is larger than the table's capacity,
		 * the dynamic table will be cleared.
		 */
		private void Add(byte[] name, byte[] value)
		{
			int headerSize = HeaderField.SizeOf(name, value);

			// Clear the table if the header field size is larger than the capacity.
			if (headerSize > this.capacity) {
				this.Clear();
				return;
			}

			// Evict oldest entries until we have enough capacity.
			while(this.size + headerSize > this.capacity) {
				this.Remove();
			}

			// Copy name and value that modifications of original do not affect the dynamic table.
			name.CopyTo(name, 0);
			value.CopyTo(value, 0);

			int h = Encoder.Hash(name);
			int i = Encoder.Index(h);
			HeaderEntry old = headerFields[i];
			HeaderEntry e = new HeaderEntry(h, name, value, head.Before.Index - 1, old);
			headerFields[i] = e;
			e.AddBefore(head);
			this.size += headerSize;
		}

		/**
		 * Remove and return the oldest header field from the dynamic table.
		 */
		private HeaderField Remove()
		{
			if (this.size == 0) {
				return null;
			}
			HeaderEntry eldest = head.After;
			int h = eldest.Hash;
			int i = Encoder.Index(h);
			HeaderEntry prev = headerFields[i];
			HeaderEntry e = prev;
			while(e != null) {
				HeaderEntry next = e.Next;
				if (e == eldest) {
					if (prev == eldest) {
						headerFields[i] = next;
					} else {
						prev.Next = next;
					}
					eldest.Remove();
					this.size -= eldest.Size;
					return eldest;
				}
				prev = e;
				e = next;
			}
			return null;
		}

		/**
		 * Remove all entries from the dynamic table.
		 */
		private void Clear()
		{
			for(int i = 0; i < headerFields.Length; i++) {
				headerFields[i] = null;
			}
			head.Before = head.After = head;
			this.size = 0;
		}

		/**
		 * Returns the hash code for the given header field name.
		 */
		private static int Hash(byte[] name)
		{
			int h = 0;
			for(int i = 0; i < name.Length; i++) {
				h = 31 * h + name[i];
			}
			if (h > 0) {
				return h;
			} else if (h == int.MinValue) {
					return int.MaxValue;
				} else {
					return -h;
				}
		}

		/**
		 * Returns the index into the hash table for the hash code h.
		 */
		private static int Index(int h)
		{
			return h % BUCKET_SIZE;
		}

		/**
		 * A linked hash map HeaderField entry.
		 */
		private class HeaderEntry : HeaderField
		{
			// These fields comprise the doubly linked list used for iteration.
			private HeaderEntry before, after;

			// These fields comprise the chained list for header fields with the same hash.
			private HeaderEntry next;
			private int hash;

			// This is used to compute the index in the dynamic table.
			private int index;

			public HeaderEntry Before { get { return this.before; } set { this.before = value; } }

			public HeaderEntry After { get { return this.after; } set { this.after = value; } }

			public HeaderEntry Next { get { return this.next; } set { this.next = value; } }

			public int Hash { get { return this.hash; } }

			public int Index { get { return this.index; } }

			/**
			 * Creates new entry.
			 */
			public HeaderEntry(int hash, byte[] name, byte[] value, int index, HeaderEntry next) : base(name, value)
			{
				this.index = index;
				this.hash = hash;
				this.next = next;
			}

			/**
			 * Removes this entry from the linked list.
			 */
			public void Remove()
			{
				before.after = after;
				after.before = before;
			}

			/**
			 * Inserts this entry before the specified existing entry in the list.
			 */
			public void AddBefore(HeaderEntry existingEntry)
			{
				after = existingEntry;
				before = existingEntry.before;
				before.after = this;
				after.before = this;
			}
		}
	}
}
