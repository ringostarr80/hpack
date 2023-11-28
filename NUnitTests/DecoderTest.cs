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

		private static string hex(string s)
		{
			return Hex.EncodeHexString(Encoding.UTF8.GetBytes(s));
		}

		private static byte[] getBytes(string s)
		{
			return Encoding.UTF8.GetBytes(s);
		}

		private void decode(string encoded)
		{
			var b = Hex.DecodeHex(encoded.ToCharArray());
			var input = new byte[b.Length];
			for(var i = 0; i < b.Length; i++) {
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
			var compressed = Hex.DecodeHex("FFF0".ToCharArray());
			var compressedInput = new byte[compressed.Length];
			for(var i = 0; i < compressed.Length; i++) {
				compressedInput[i] = (byte)compressed[i];
			}
			using(var input = new BinaryReader(new MemoryStream(compressedInput))) {
				this.decoder.Decode(input, this.mockListener);
				Assert.That(input.BaseStream.Length - input.BaseStream.Position, Is.EqualTo(1));
				this.decoder.Decode(input, this.mockListener);
				Assert.That(input.BaseStream.Length - input.BaseStream.Position, Is.EqualTo(1));
				Assert.That(this.decoder.Length(), Is.Zero);
				Assert.That(this.decoder.Size(), Is.Zero);
			}
		}

		[Test]
		public void testUnusedIndex()
		{
			// Index 0 is not used
			Assert.Throws<IOException>(delegate { this.decode("80"); });
		}

		[Test]
		public void testIllegalIndex()
		{
			// Index larger than the header table
			Assert.Throws<IOException>(delegate { this.decode("FF00"); });
		}

		[Test]
		public void testInsidiousIndex()
		{
			// Insidious index so the last shift causes sign overflow
			Assert.Throws<IOException>(delegate { this.decode("FF8080808008"); });
		}

		[Test]
		public void testDynamicTableSizeUpdate()
		{
			this.decode("20");
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			this.decode("3FE11F");
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.EqualTo(4096));
		}

		[Test]
		public void testDynamicTableSizeUpdateRequired()
		{
			this.decoder.SetMaxHeaderTableSize(32);
			this.decode("3F00");
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.EqualTo(31));
		}

		[Test]
		public void testIllegalDynamicTableSizeUpdate()
		{
			// max header table size = MAX_HEADER_TABLE_SIZE + 1
			Assert.Throws<IOException>(delegate { this.decode("3FE21F"); });
		}

		[Test]
		public void testInsidiousMaxDynamicTableSize()
		{
			// max header table size sign overflow
			Assert.Throws<IOException>(delegate { this.decode("3FE1FFFFFF07"); });
		}

		[Test]
		public void testReduceMaxDynamicTableSize()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			this.decode("2081");
		}

		[Test]
		public void testTooLargeDynamicTableSizeUpdate()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			Assert.Throws<IOException>(delegate { this.decode("21"); }); // encoder max header table size not small enough
		}

		[Test]
		public void testMissingDynamicTableSizeUpdate()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			Assert.Throws<IOException>(delegate { this.decode("81"); });
		}

		[Test]
		public void testLiteralWithIncrementalIndexingWithEmptyName()
		{
			Assert.Throws<IOException>(delegate { this.decode("000005" + hex("value")); });
		}

		[Test]
		public void testLiteralWithIncrementalIndexingCompleteEviction()
		{
			// Verify indexed host header
			this.decode("4004" + hex("name") + "05" + hex("value"));
			//verify(this.mockListener).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
			Assert.That(decoder.EndHeaderBlock(), Is.False);

			//reset(this.mockListener);
			var sb = new StringBuilder();
			for(var i = 0; i < 4096; i++) {
				sb.Append('a');
			}
			//String value = sb.ToString();
			sb = new StringBuilder();
			sb.Append("417F811F");
			for(var i = 0; i < 4096; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verify(this.mockListener).addHeader(getBytes(":authority"), getBytes(value), false);
			//verifyNoMoreInteractions(this.mockListener);
			Assert.That(decoder.EndHeaderBlock(), Is.False);

			// Verify next header is inserted at index 62
			this.decode("4004" + hex("name") + "05" + hex("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void testLiteralWithIncrementalIndexingWithLargeName()
		{
			// Ignore header name that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("407F817F");
			for(var i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify next header is inserted at index 62
			this.decode("4004" + hex("name") + "05" + hex("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void testLiteralWithIncrementalIndexingWithLargeValue()
		{
			// Ignore header that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("4004");
			sb.Append(hex("name"));
			sb.Append("7F813F");
			for(var i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify next header is inserted at index 62
			this.decode("4004" + hex("name") + "05" + hex("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void testLiteralWithoutIndexingWithEmptyName()
		{
			Assert.Throws<IOException>(delegate { this.decode("000005" + hex("value")); });
		}

		[Test]
		public void testLiteralWithoutIndexingWithLargeName()
		{
			// Ignore header name that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("007F817F");
			for(var i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>(delegate { this.decode("BE"); });
		}

		[Test]
		public void testLiteralWithoutIndexingWithLargeValue()
		{
			// Ignore header that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("0004");
			sb.Append(hex("name"));
			sb.Append("7F813F");
			for(var i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>(delegate { this.decode("BE"); });
		}

		[Test]
		public void testLiteralNeverIndexedWithEmptyName()
		{
			Assert.Throws<IOException>(delegate { this.decode("100005" + hex("value")); });
		}

		[Test]
		public void testLiteralNeverIndexedWithLargeName()
		{
			// Ignore header name that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("107F817F");
			for(var i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>(delegate { this.decode("BE"); });
		}

		[Test]
		public void testLiteralNeverIndexedWithLargeValue()
		{
			// Ignore header that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("1004");
			sb.Append(hex("name"));
			sb.Append("7F813F");
			for(var i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			this.decode(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>(delegate { this.decode("BE"); });
		}
	}
}
