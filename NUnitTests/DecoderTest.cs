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
using System.Text;
using NUnit.Framework;

namespace hpack
{
	public class HeaderListener : IHeaderListener
	{
		public void AddHeader(byte[] name, byte[] value, bool sensitive)
		{
			//Console.WriteLine("HeaderListener.AddHeader(" + Encoding.UTF8.GetString(name) + ", " + Encoding.UTF8.GetString(value) + ")");
		}
	}

	[TestFixture]
	public class DecoderTest
	{
		private static int MAX_HEADER_SIZE = 8192;
		private static int MAX_HEADER_TABLE_SIZE = 4096;

		private Decoder decoder;
		private IHeaderListener mockListener;

		private static String hex(string s)
		{
			return Hex.EncodeHexString(Encoding.UTF8.GetBytes(s));
		}

		private static byte[] getBytes(string s)
		{
			return HpackUtil.ISO_8859_1.GetBytes(s);
		}

		private void decode(string encoded)
		{
			sbyte[] b = Hex.DecodeHex(encoded.ToCharArray());
			byte[] input = new byte[b.Length];
			for(int i = 0; i < b.Length; i++) {
				input[i] = (byte)b[i];
			}
			this.decoder.Decode(new BinaryReader(new MemoryStream(input)), this.mockListener);
		}

		[SetUp]
		public void SetUp()
		{
			this.decoder = new Decoder(MAX_HEADER_SIZE, MAX_HEADER_TABLE_SIZE);
			//this.mockListener = mock(HeaderListener.class);
			this.mockListener = new HeaderListener();
		}

		[Test]
		public void testIncompleteIndex()
		{
			// Verify incomplete indices are unread
			sbyte[] compressed = Hex.DecodeHex("FFF0".ToCharArray());
			byte[] compressedInput = new byte[compressed.Length];
			for(int i = 0; i < compressed.Length; i++) {
				compressedInput[i] = (byte)compressed[i];
			}
			using(var input = new BinaryReader(new MemoryStream(compressedInput))) {
				this.decoder.Decode(input, this.mockListener);
				Assert.AreEqual(1, input.BaseStream.Length - input.BaseStream.Position);
				this.decoder.Decode(input, this.mockListener);
				Assert.AreEqual(1, input.BaseStream.Length - input.BaseStream.Position);
			}
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testUnusedIndex()
		{
			// Index 0 is not used
			this.decode("80");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testIllegalIndex()
		{
			// Index larger than the header table
			this.decode("FF00");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testInsidiousIndex()
		{
			// Insidious index so the last shift causes sign overflow
			this.decode("FF8080808008");
		}

		[Test]
		public void testDynamicTableSizeUpdate()
		{
			this.decode("20");
			Assert.AreEqual(0, decoder.GetMaxHeaderTableSize());
			this.decode("3FE11F");
			Assert.AreEqual(4096, decoder.GetMaxHeaderTableSize());
		}

		[Test]
		public void testDynamicTableSizeUpdateRequired()
		{
			this.decoder.SetMaxHeaderTableSize(32);
			this.decode("3F00");
			Assert.AreEqual(31, decoder.GetMaxHeaderTableSize());
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testIllegalDynamicTableSizeUpdate()
		{
			// max header table size = MAX_HEADER_TABLE_SIZE + 1
			this.decode("3FE21F");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testInsidiousMaxDynamicTableSize()
		{
			// max header table size sign overflow
			this.decode("3FE1FFFFFF07");
		}

		[Test]
		public void testReduceMaxDynamicTableSize()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.AreEqual(0, decoder.GetMaxHeaderTableSize());
			this.decode("2081");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testTooLargeDynamicTableSizeUpdate()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.AreEqual(0, decoder.GetMaxHeaderTableSize());
			this.decode("21"); // encoder max header table size not small enough
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testMissingDynamicTableSizeUpdate()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.AreEqual(0, decoder.GetMaxHeaderTableSize());
			this.decode("81");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testLiteralWithIncrementalIndexingWithEmptyName()
		{
			this.decode("000005" + hex("value"));
		}

		[Test]
		public void testLiteralWithIncrementalIndexingCompleteEviction()
		{
			// Verify indexed host header
			this.decode("4004" + hex("name") + "05" + hex("value"));
			//verify(this.mockListener).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
			Assert.IsFalse(decoder.EndHeaderBlock());

			//reset(this.mockListener);
			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < 4096; i++) {
				sb.Append("a");
			}
			//String value = sb.ToString();
			sb = new StringBuilder();
			sb.Append("417F811F");
			for(int i = 0; i < 4096; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verify(this.mockListener).addHeader(getBytes(":authority"), getBytes(value), false);
			//verifyNoMoreInteractions(this.mockListener);
			Assert.IsFalse(decoder.EndHeaderBlock());

			// Verify next header is inserted at index 62
			this.decode("4004" + hex("name") + "05" + hex("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void testLiteralWithIncrementalIndexingWithLargeName()
		{
			// Ignore header name that exceeds max header size
			StringBuilder sb = new StringBuilder();
			sb.Append("407F817F");
			for(int i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.IsTrue(decoder.EndHeaderBlock());

			// Verify next header is inserted at index 62
			this.decode("4004" + hex("name") + "05" + hex("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void testLiteralWithIncrementalIndexingWithLargeValue()
		{
			// Ignore header that exceeds max header size
			StringBuilder sb = new StringBuilder();
			sb.Append("4004");
			sb.Append(hex("name"));
			sb.Append("7F813F");
			for(int i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.IsTrue(decoder.EndHeaderBlock());

			// Verify next header is inserted at index 62
			this.decode("4004" + hex("name") + "05" + hex("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testLiteralWithoutIndexingWithEmptyName()
		{
			this.decode("000005" + hex("value"));
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testLiteralWithoutIndexingWithLargeName()
		{
			// Ignore header name that exceeds max header size
			StringBuilder sb = new StringBuilder();
			sb.Append("007F817F");
			for(int i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.IsTrue(decoder.EndHeaderBlock());

			// Verify table is unmodified
			this.decode("BE");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testLiteralWithoutIndexingWithLargeValue()
		{
			// Ignore header that exceeds max header size
			StringBuilder sb = new StringBuilder();
			sb.Append("0004");
			sb.Append(hex("name"));
			sb.Append("7F813F");
			for(int i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.IsTrue(decoder.EndHeaderBlock());

			// Verify table is unmodified
			this.decode("BE");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testLiteralNeverIndexedWithEmptyName()
		{
			this.decode("100005" + hex("value"));
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testLiteralNeverIndexedWithLargeName()
		{
			// Ignore header name that exceeds max header size
			StringBuilder sb = new StringBuilder();
			sb.Append("107F817F");
			for(int i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.IsTrue(decoder.EndHeaderBlock());

			// Verify table is unmodified
			this.decode("BE");
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void testLiteralNeverIndexedWithLargeValue()
		{
			// Ignore header that exceeds max header size
			StringBuilder sb = new StringBuilder();
			sb.Append("1004");
			sb.Append(hex("name"));
			sb.Append("7F813F");
			for(int i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.IsTrue(decoder.EndHeaderBlock());

			// Verify table is unmodified
			this.decode("BE");
		}
	}
}
