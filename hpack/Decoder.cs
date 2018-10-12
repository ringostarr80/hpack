/*
 * Copyright 2014 Twitter, Inc
 * This file is a derivative work modified by Ringo Leese
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
	/// <summary>
	/// The Decoder class.
	/// </summary>
	public class Decoder
	{
		private static byte[] EMPTY = { };

		private DynamicTable dynamicTable;

		private int maxHeaderSize;
		private int maxDynamicTableSize;
		private int encoderMaxDynamicTableSize;
		private bool maxDynamicTableSizeChangeRequired;

		private long headerSize;
		private State state;
		private HpackUtil.IndexType indexType;
		private int index;
		private bool huffmanEncoded;
		private int skipLength;
		private int nameLength;
		private int valueLength;
		private byte[] name;

		/// <summary>
		/// The State enum.
		/// </summary>
		public enum State
		{
			/// <summary>
			/// Read header represenation state.
			/// </summary>
			READ_HEADER_REPRESENTATION,
			/// <summary>
			/// Read max dynamic table size state.
			/// </summary>
			READ_MAX_DYNAMIC_TABLE_SIZE,
			/// <summary>
			/// Read indexed header state.
			/// </summary>
			READ_INDEXED_HEADER,
			/// <summary>
			/// Read indexed header name state.
			/// </summary>
			READ_INDEXED_HEADER_NAME,
			/// <summary>
			/// Read literal header name length prefix state.
			/// </summary>
			READ_LITERAL_HEADER_NAME_LENGTH_PREFIX,
			/// <summary>
			/// Read literal header name length state.
			/// </summary>
			READ_LITERAL_HEADER_NAME_LENGTH,
			/// <summary>
			/// Read literal header name state.
			/// </summary>
			READ_LITERAL_HEADER_NAME,
			/// <summary>
			/// Skip literal header name state.
			/// </summary>
			SKIP_LITERAL_HEADER_NAME,
			/// <summary>
			/// Read literal header value length prefix state.
			/// </summary>
			READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX,
			/// <summary>
			/// Read literal header value length state.
			/// </summary>
			READ_LITERAL_HEADER_VALUE_LENGTH,
			/// <summary>
			/// Read literal header value state.
			/// </summary>
			READ_LITERAL_HEADER_VALUE,
			/// <summary>
			/// Skip literal header value state.
			/// </summary>
			SKIP_LITERAL_HEADER_VALUE
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="hpack.Decoder"/> class.
		/// </summary>
		/// <param name="maxHeaderSize">Max header size.</param>
		/// <param name="maxHeaderTableSize">Max header table size.</param>
		public Decoder(int maxHeaderSize, int maxHeaderTableSize)
		{
			this.dynamicTable = new DynamicTable(maxHeaderTableSize);
			this.maxHeaderSize = maxHeaderSize;
			this.maxDynamicTableSize = maxHeaderTableSize;
			this.encoderMaxDynamicTableSize = maxHeaderTableSize;
			this.maxDynamicTableSizeChangeRequired = false;
			this.Reset();
		}

		private void Reset()
		{
			this.headerSize = 0;
			this.state = State.READ_HEADER_REPRESENTATION;
			this.indexType = HpackUtil.IndexType.NONE;
		}

		/// <summary>
		/// Decode the header block into header fields.
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="headerListener">Header listener.</param>
		public void Decode(BinaryReader input, IHeaderListener headerListener)
		{
			while(input.BaseStream.Length - input.BaseStream.Position > 0) {
				switch(this.state) {
					case State.READ_HEADER_REPRESENTATION:
						var b = input.ReadSByte();
						if (maxDynamicTableSizeChangeRequired && (b & 0xE0) != 0x20) {
							// Encoder MUST signal maximum dynamic table size change
							throw new IOException("max dynamic table size change required");
						}
						if (b < 0) {
							// Indexed Header Field
							this.index = b & 0x7F;
							if (this.index == 0) {
								throw new IOException("illegal index value (" + this.index + ")");
							} else if (this.index == 0x7F) {
									this.state = State.READ_INDEXED_HEADER;
								} else {
									this.IndexHeader(this.index, headerListener);
								}
						} else if ((b & 0x40) == 0x40) {
								// Literal Header Field with Incremental Indexing
								this.indexType = HpackUtil.IndexType.INCREMENTAL;
								this.index = b & 0x3F;
								if (this.index == 0) {
									this.state = State.READ_LITERAL_HEADER_NAME_LENGTH_PREFIX;
								} else if (this.index == 0x3F) {
										this.state = State.READ_INDEXED_HEADER_NAME;
									} else {
										// Index was stored as the prefix
										this.ReadName(this.index);
										this.state = State.READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
									}
							} else if ((b & 0x20) == 0x20) {
									// Dynamic Table Size Update
									this.index = b & 0x1F;
									if (this.index == 0x1F) {
										this.state = State.READ_MAX_DYNAMIC_TABLE_SIZE;
									} else {
										this.SetDynamicTableSize(index);
										this.state = State.READ_HEADER_REPRESENTATION;
									}
								} else {
									// Literal Header Field without Indexing / never Indexed
									this.indexType = ((b & 0x10) == 0x10) ? HpackUtil.IndexType.NEVER : HpackUtil.IndexType.NONE;
									this.index = b & 0x0F;
									if (this.index == 0) {
										this.state = State.READ_LITERAL_HEADER_NAME_LENGTH_PREFIX;
									} else if (this.index == 0x0F) {
											this.state = State.READ_INDEXED_HEADER_NAME;
										} else {
											// Index was stored as the prefix
											this.ReadName(this.index);
											this.state = State.READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
										}
								}
						break;

					case State.READ_MAX_DYNAMIC_TABLE_SIZE:
						var maxSize = Decoder.DecodeULE128(input);
						if (maxSize == -1) {
							return;
						}

						// Check for numerical overflow
						if (maxSize > int.MaxValue - this.index) {
							throw new IOException("decompression failure");
						}

						this.SetDynamicTableSize(this.index + maxSize);
						this.state = State.READ_HEADER_REPRESENTATION;
						break;

					case State.READ_INDEXED_HEADER:
						var headerIndex = Decoder.DecodeULE128(input);
						if (headerIndex == -1) {
							return;
						}

						// Check for numerical overflow
						if (headerIndex > int.MaxValue - this.index) {
							throw new IOException("decompression failure");
						}

						this.IndexHeader(this.index + headerIndex, headerListener);
						this.state = State.READ_HEADER_REPRESENTATION;
						break;

					case State.READ_INDEXED_HEADER_NAME:
						// Header Name matches an entry in the Header Table
						var nameIndex = Decoder.DecodeULE128(input);
						if (nameIndex == -1) {
							return;
						}

						// Check for numerical overflow
						if (nameIndex > int.MaxValue - this.index) {
							throw new IOException("decompression failure");
						}

						this.ReadName(this.index + nameIndex);
						this.state = State.READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
						break;

					case State.READ_LITERAL_HEADER_NAME_LENGTH_PREFIX:
						b = input.ReadSByte();
						this.huffmanEncoded = (b & 0x80) == 0x80;
						this.index = b & 0x7F;
						if (this.index == 0x7f) {
							this.state = State.READ_LITERAL_HEADER_NAME_LENGTH;
						} else {
							this.nameLength = this.index;

							// Disallow empty names -- they cannot be represented in HTTP/1.x
							if (this.nameLength == 0) {
								throw new IOException("decompression failure");
							}

							// Check name length against max header size
							if (this.ExceedsMaxHeaderSize(this.nameLength)) {
								if (this.indexType == HpackUtil.IndexType.NONE) {
									// Name is unused so skip bytes
									this.name = EMPTY;
									this.skipLength = this.nameLength;
									this.state = State.SKIP_LITERAL_HEADER_NAME;
									break;
								}

								// Check name length against max dynamic table size
								if (this.nameLength + HeaderField.HEADER_ENTRY_OVERHEAD > this.dynamicTable.Capacity) {
									this.dynamicTable.Clear();
									this.name = EMPTY;
									this.skipLength = this.nameLength;
									this.state = State.SKIP_LITERAL_HEADER_NAME;
									break;
								}
							}
							this.state = State.READ_LITERAL_HEADER_NAME;
						}
						break;

					case State.READ_LITERAL_HEADER_NAME_LENGTH:
						// Header Name is a Literal String
						this.nameLength = Decoder.DecodeULE128(input);
						if (this.nameLength == -1) {
							return;
						}

						// Check for numerical overflow
						if (this.nameLength > int.MaxValue - this.index) {
							throw new IOException("decompression failure");
						}
						this.nameLength += this.index;

						// Check name length against max header size
						if (this.ExceedsMaxHeaderSize(this.nameLength)) {
							if (this.indexType == HpackUtil.IndexType.NONE) {
								// Name is unused so skip bytes
								this.name = EMPTY;
								this.skipLength = this.nameLength;
								this.state = State.SKIP_LITERAL_HEADER_NAME;
								break;
							}

							// Check name length against max dynamic table size
							if (this.nameLength + HeaderField.HEADER_ENTRY_OVERHEAD > this.dynamicTable.Capacity) {
								this.dynamicTable.Clear();
								this.name = EMPTY;
								this.skipLength = this.nameLength;
								this.state = State.SKIP_LITERAL_HEADER_NAME;
								break;
							}
						}
						this.state = State.READ_LITERAL_HEADER_NAME;
						break;

					case State.READ_LITERAL_HEADER_NAME:
						// Wait until entire name is readable
						if (input.BaseStream.Length - input.BaseStream.Position < this.nameLength) {
							return;
						}

						this.name = this.ReadStringLiteral(input, this.nameLength);
						this.state = State.READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
						break;

					case State.SKIP_LITERAL_HEADER_NAME:
						
						this.skipLength -= (int)input.BaseStream.Seek(this.skipLength, SeekOrigin.Current);
						if (this.skipLength < 0) {
							this.skipLength = 0;
						}
						if (this.skipLength == 0) {
							this.state = State.READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
						}
						break;

					case State.READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX:
						b = input.ReadSByte();
						this.huffmanEncoded = (b & 0x80) == 0x80;
						this.index = b & 0x7F;
						if (this.index == 0x7f) {
							this.state = State.READ_LITERAL_HEADER_VALUE_LENGTH;
						} else {
							this.valueLength = this.index;

							// Check new header size against max header size
							var newHeaderSize1 = (long)((long)this.nameLength + (long)this.valueLength);
							if (this.ExceedsMaxHeaderSize(newHeaderSize1)) {
								// truncation will be reported during endHeaderBlock
								this.headerSize = this.maxHeaderSize + 1;

								if (this.indexType == HpackUtil.IndexType.NONE) {
									// Value is unused so skip bytes
									this.state = State.SKIP_LITERAL_HEADER_VALUE;
									break;
								}

								// Check new header size against max dynamic table size
								if (newHeaderSize1 + HeaderField.HEADER_ENTRY_OVERHEAD > this.dynamicTable.Capacity) {
									this.dynamicTable.Clear();
									this.state = State.SKIP_LITERAL_HEADER_VALUE;
									break;
								}
							}

							if (this.valueLength == 0) {
								this.InsertHeader(headerListener, this.name, EMPTY, this.indexType);
								this.state = State.READ_HEADER_REPRESENTATION;
							} else {
								this.state = State.READ_LITERAL_HEADER_VALUE;
							}
						}
						break;

					case State.READ_LITERAL_HEADER_VALUE_LENGTH:
						// Header Value is a Literal String
						this.valueLength = Decoder.DecodeULE128(input);
						if (this.valueLength == -1) {
							return;
						}

						// Check for numerical overflow
						if (this.valueLength > int.MaxValue - this.index) {
							throw new IOException("decompression failure");
						}
						this.valueLength += this.index;

						// Check new header size against max header size
						var newHeaderSize2 = (long)((long)this.nameLength + (long)this.valueLength);
						if (newHeaderSize2 + this.headerSize > this.maxHeaderSize) {
							// truncation will be reported during endHeaderBlock
							this.headerSize = this.maxHeaderSize + 1;

							if (this.indexType == HpackUtil.IndexType.NONE) {
								// Value is unused so skip bytes
								this.state = State.SKIP_LITERAL_HEADER_VALUE;
								break;
							}

							// Check new header size against max dynamic table size
							if (newHeaderSize2 + HeaderField.HEADER_ENTRY_OVERHEAD > this.dynamicTable.Capacity) {
								this.dynamicTable.Clear();
								this.state = State.SKIP_LITERAL_HEADER_VALUE;
								break;
							}
						}
						this.state = State.READ_LITERAL_HEADER_VALUE;
						break;

					case State.READ_LITERAL_HEADER_VALUE:
						// Wait until entire value is readable
						if (input.BaseStream.Length - input.BaseStream.Position < this.valueLength) {
							return;
						}

						var value = this.ReadStringLiteral(input, this.valueLength);
						this.InsertHeader(headerListener, this.name, value, this.indexType);
						this.state = State.READ_HEADER_REPRESENTATION;
						break;

					case State.SKIP_LITERAL_HEADER_VALUE:
						this.valueLength -= (int)input.BaseStream.Seek(this.valueLength, SeekOrigin.Current);
						if (this.valueLength < 0) {
							this.valueLength = 0;
						}
						if (this.valueLength == 0) {
							this.state = State.READ_HEADER_REPRESENTATION;
						}
						break;

					default:
						throw new Exception("should not reach here");
				}
			}
		}

		/// <summary>
		/// End the current header block. Returns if the header field has been truncated.
		/// This must be called after the header block has been completely decoded.
		/// </summary>
		/// <returns><c>true</c>, if header block was ended, <c>false</c> otherwise.</returns>
		public bool EndHeaderBlock()
		{
			var truncated = (headerSize > maxHeaderSize) ? true : false;
			this.Reset();
			return truncated;
		}

		/// <summary>
		/// Set the maximum table size.
		/// If this is below the maximum size of the dynamic table used by the encoder,
		/// the beginning of the next header block MUST signal this change.
		/// </summary>
		/// <param name="maxHeaderTableSize">Max header table size.</param>
		public void SetMaxHeaderTableSize(int maxHeaderTableSize)
		{
			maxDynamicTableSize = maxHeaderTableSize;
			if (maxDynamicTableSize < encoderMaxDynamicTableSize) {
				// decoder requires less space than encoder
				// encoder MUST signal this change
				this.maxDynamicTableSizeChangeRequired = true;
				this.dynamicTable.SetCapacity(maxDynamicTableSize);
			}
		}

		/// <summary>
		/// Return the maximum table size.
		/// This is the maximum size allowed by both the encoder and the decoder.
		/// </summary>
		/// <returns>The max header table size.</returns>
		public int GetMaxHeaderTableSize()
		{
			return this.dynamicTable.Capacity;
		}

		/// <summary>
		/// Return the number of header fields in the dynamic table.
		/// Exposed for testing.
		/// </summary>
		int Length()
		{
			return this.dynamicTable.Length();
		}

		/// <summary>
		/// Return the size of the dynamic table.
		/// Exposed for testing.
		/// </summary>
		int Size()
		{
			return this.dynamicTable.Size;
		}

		/// <summary>
		/// Return the header field at the given index.
		/// Exposed for testing.
		/// </summary>
		/// <returns>The header field.</returns>
		/// <param name="index">Index.</param>
		HeaderField GetHeaderField(int index)
		{
			return this.dynamicTable.GetEntry(index + 1);
		}

		private void SetDynamicTableSize(int dynamicTableSize)
		{
			if (dynamicTableSize > this.maxDynamicTableSize) {
				throw new IOException("invalid max dynamic table size");
			}
			this.encoderMaxDynamicTableSize = dynamicTableSize;
			this.maxDynamicTableSizeChangeRequired = false;
			this.dynamicTable.SetCapacity(dynamicTableSize);
		}

		private void ReadName(int index)
		{
			if (index <= StaticTable.Length) {
				var headerField = StaticTable.GetEntry(index);
				this.name = headerField.Name;
			} else if (index - StaticTable.Length <= this.dynamicTable.Length()) {
					var headerField = this.dynamicTable.GetEntry(index - StaticTable.Length);
					this.name = headerField.Name;
				} else {
					throw new IOException("illegal index value (" + index + ")");
				}
		}

		private void IndexHeader(int index, IHeaderListener headerListener)
		{
			if (index <= StaticTable.Length) {
				var headerField = StaticTable.GetEntry(index);
				this.AddHeader(headerListener, headerField.Name, headerField.Value, false);
			} else if (index - StaticTable.Length <= this.dynamicTable.Length()) {
					var headerField = this.dynamicTable.GetEntry(index - StaticTable.Length);
					this.AddHeader(headerListener, headerField.Name, headerField.Value, false);
				} else {
					throw new IOException("illegal index value (" + index + ")");
				}
		}

		private void InsertHeader(IHeaderListener headerListener, byte[] name, byte[] value, HpackUtil.IndexType indexType)
		{
			this.AddHeader(headerListener, name, value, indexType == HpackUtil.IndexType.NEVER);

			switch(indexType) {
				case HpackUtil.IndexType.NONE:
				case HpackUtil.IndexType.NEVER:
					break;

				case HpackUtil.IndexType.INCREMENTAL:
					this.dynamicTable.Add(new HeaderField(name, value));
					break;

				default:
					throw new Exception("should not reach here");
			}
		}

		private void AddHeader(IHeaderListener headerListener, byte[] name, byte[] value, bool sensitive)
		{
			if (name.Length == 0) {
				throw new ArgumentException("name is empty");
			}
			var newSize = (long)(this.headerSize + name.Length + value.Length);
			if (newSize <= this.maxHeaderSize) {
				headerListener.AddHeader(name, value, sensitive);
				this.headerSize = (int)newSize;
			} else {
				// truncation will be reported during endHeaderBlock
				this.headerSize = this.maxHeaderSize + 1;
			}
		}

		private bool ExceedsMaxHeaderSize(long size)
		{
			// Check new header size against max header size
			if (size + this.headerSize <= this.maxHeaderSize) {
				return false;
			}

			// truncation will be reported during endHeaderBlock
			this.headerSize = this.maxHeaderSize + 1;
			return true;
		}

		private byte[] ReadStringLiteral(BinaryReader input, int length)
		{
			var buf = new byte[length];
			var lengthToRead = length;
			if (input.BaseStream.Length - input.BaseStream.Position < length) {
				lengthToRead = (int)input.BaseStream.Length - (int)input.BaseStream.Position;
			}
			var readBytes = input.Read(buf, 0, lengthToRead);
			if (readBytes != length) {
				throw new IOException("decompression failure");
			}

			if (this.huffmanEncoded) {
				return Huffman.DECODER.Decode(buf);
			} else {
				return buf;
			}
		}

		// Unsigned Little Endian Base 128 Variable-Length Integer Encoding
		private static int DecodeULE128(BinaryReader input)
		{
			var markedPosition = input.BaseStream.Position;
			var result = 0;
			var shift = 0;
			while(shift < 32) {
				if (input.BaseStream.Length - input.BaseStream.Position == 0) {
					// Buffer does not contain entire integer,
					// reset reader index and return -1.
					input.BaseStream.Position = markedPosition;
					return -1;
				}
				var b = input.ReadSByte();
				if (shift == 28 && (b & 0xF8) != 0) {
					break;
				}
				result |= (b & 0x7F) << shift;
				if ((b & 0x80) == 0) {
					return result;
				}
				shift += 7;
			}
			// Value exceeds Integer.MAX_VALUE
			input.BaseStream.Position = markedPosition;
			throw new IOException("decompression failure");
		}
	}
}
