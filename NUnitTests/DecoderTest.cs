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
using System.Text;

using hpack;
using NUnit.Framework;

namespace NUnitTests
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
		private static readonly int MAX_HEADER_SIZE = 8192;
		private static readonly int MAX_HEADER_TABLE_SIZE = 4096;

		private hpack.Decoder decoder;
		private IHeaderListener mockListener;

		private static string ToHexUtf8(string s)
		{
			return Hex.EncodeHexString(Encoding.UTF8.GetBytes(s));
		}

		private void DecodeHexInput(string encoded)
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
			this.decoder = new hpack.Decoder(MAX_HEADER_SIZE, MAX_HEADER_TABLE_SIZE);
			//this.mockListener = mock(HeaderListener.class);
			this.mockListener = new HeaderListener();
		}

		[Test]
		public void TestIncompleteIndex()
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
		public void TestUnusedIndex()
		{
			// Index 0 is not used
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("80")));
		}

		[Test]
		public void TestIllegalIndex()
		{
			// Index larger than the header table
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("FF00")));
		}

		[Test]
		public void TestInsidiousIndex()
		{
			// Insidious index so the last shift causes sign overflow
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("FF8080808008")));
		}

		[Test]
		public void TestDynamicTableSizeUpdate()
		{
			DecodeHexInput("20");
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			DecodeHexInput("3FE11F");
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.EqualTo(4096));
		}

		[Test]
		public void TestDynamicTableSizeUpdateRequired()
		{
			this.decoder.SetMaxHeaderTableSize(32);
			DecodeHexInput("3F00");
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.EqualTo(31));
		}

		[Test]
		public void TestIllegalDynamicTableSizeUpdate()
		{
			// max header table size = MAX_HEADER_TABLE_SIZE + 1
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("3FE21F")));
		}

		[Test]
		public void TestInsidiousMaxDynamicTableSize()
		{
			// max header table size sign overflow
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("3FE1FFFFFF07")));
		}

		[Test]
		public void TestReduceMaxDynamicTableSize()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			DecodeHexInput("2081");
		}

		[Test]
		public void TestTooLargeDynamicTableSizeUpdate()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("21"))); // encoder max header table size not small enough
		}

		[Test]
		public void TestMissingDynamicTableSizeUpdate()
		{
			this.decoder.SetMaxHeaderTableSize(0);
			Assert.That(decoder.GetMaxHeaderTableSize(), Is.Zero);
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("81")));
		}

		[Test]
		public void TestLiteralWithIncrementalIndexingWithEmptyName()
		{
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("000005" + ToHexUtf8("value"))));
		}

		[Test]
		public void TestLiteralWithIncrementalIndexingCompleteEviction()
		{
			// Verify indexed host header
			DecodeHexInput("4004" + ToHexUtf8("name") + "05" + ToHexUtf8("value"));
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
			DecodeHexInput(sb.ToString());
			//verify(this.mockListener).addHeader(getBytes(":authority"), getBytes(value), false);
			//verifyNoMoreInteractions(this.mockListener);
			Assert.That(decoder.EndHeaderBlock(), Is.False);

			// Verify next header is inserted at index 62
			DecodeHexInput("4004" + ToHexUtf8("name") + "05" + ToHexUtf8("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void TestLiteralWithIncrementalIndexingWithLargeName()
		{
			// Ignore header name that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("407F817F");
			for(var i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			DecodeHexInput(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify next header is inserted at index 62
			DecodeHexInput("4004" + ToHexUtf8("name") + "05" + ToHexUtf8("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void TestLiteralWithIncrementalIndexingWithLargeValue()
		{
			// Ignore header that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("4004");
			sb.Append(ToHexUtf8("name"));
			sb.Append("7F813F");
			for(var i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			DecodeHexInput(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify next header is inserted at index 62
			DecodeHexInput("4004" + ToHexUtf8("name") + "05" + ToHexUtf8("value") + "BE");
			//verify(this.mockListener, times(2)).addHeader(getBytes("name"), getBytes("value"), false);
			//verifyNoMoreInteractions(this.mockListener);
		}

		[Test]
		public void TestLiteralWithoutIndexingWithEmptyName()
		{
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("000005" + ToHexUtf8("value"))));
		}

		[Test]
		public void TestLiteralWithoutIndexingWithLargeName()
		{
			// Ignore header name that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("007F817F");
			for(var i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			DecodeHexInput(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("BE")));
		}

		[Test]
		public void TestLiteralWithoutIndexingWithLargeValue()
		{
			// Ignore header that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("0004");
			sb.Append(ToHexUtf8("name"));
			sb.Append("7F813F");
			for(var i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			DecodeHexInput(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("BE")));
		}

		[Test]
		public void TestLiteralNeverIndexedWithEmptyName()
		{
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("100005" + ToHexUtf8("value"))));
		}

		[Test]
		public void TestLiteralNeverIndexedWithLargeName()
		{
			// Ignore header name that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("107F817F");
			for(var i = 0; i < 16384; i++) {
				sb.Append("61"); // 'a'
			}
			sb.Append("00");
			DecodeHexInput(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("BE")));
		}

		[Test]
		public void TestLiteralNeverIndexedWithLargeValue()
		{
			// Ignore header that exceeds max header size
			var sb = new StringBuilder();
			sb.Append("1004");
			sb.Append(ToHexUtf8("name"));
			sb.Append("7F813F");
			for(var i = 0; i < 8192; i++) {
				sb.Append("61"); // 'a'
			}
			DecodeHexInput(sb.ToString());
			//verifyNoMoreInteractions(this.mockListener);

			// Verify header block is reported as truncated
			Assert.That(decoder.EndHeaderBlock(), Is.True);

			// Verify table is unmodified
			Assert.Throws<IOException>((Action)(() => DecodeHexInput("BE")));
		}
	}
}
