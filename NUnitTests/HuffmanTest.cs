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
using System.Linq;
using System.Text;

using hpack;
using NUnit.Framework;

namespace NUnitTests
{
	public class HuffmanTest
	{
		[Test]
		public void TestHuffman()
		{
			var s = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			for(var i = 0; i < s.Length; i++) {
				RoundTrip(s[..i]);
			}

			var random = new Random(123456789); // DevSkim: ignore DS148264
			var buf = new byte[4096];
			random.NextBytes(buf);
			RoundTrip(buf);
		}

		[Test]
		public void TestDecodeEOS()
		{
			var buf = new byte[4];
			for(var i = 0; i < 4; i++) {
				buf[i] = (byte)0xFF;
			}
			Assert.Throws<IOException>((Action)(() => Huffman.DECODER.Decode(buf)));
		}

		[Test]
		public void TestDecodeIllegalPadding()
		{
			var buf = new byte[1];
			buf[0] = 0x00; // '0', invalid padding
			Assert.Throws<IOException>((Action)(() => Huffman.DECODER.Decode(buf)));
		}

		[Test]
		public void TestDecodeExtraPadding()
		{
			var buf = new byte[2];
			buf[0] = 0x0F; // '1', 'EOS'
			buf[1] = (byte)0xFF; // 'EOS'
			var decoded = Huffman.DECODER.Decode(buf);
			Assert.That(decoded, Is.EqualTo(new byte[] { 0x31 }));
		}

		private static void RoundTrip(String s)
		{
			RoundTrip(Huffman.ENCODER, Huffman.DECODER, s);
		}

		private static void RoundTrip(HuffmanEncoder encoder, HuffmanDecoder decoder, string s)
		{
			RoundTrip(encoder, decoder, Encoding.UTF8.GetBytes(s));
		}

		private static void RoundTrip(byte[] buf)
		{
			RoundTrip(Huffman.ENCODER, Huffman.DECODER, buf);
		}

		private static void RoundTrip(HuffmanEncoder encoder, HuffmanDecoder decoder, byte[] buf)
		{
            using var baos = new MemoryStream();
            using var dos = new BinaryWriter(baos);
            encoder.Encode(dos, buf);
            var actualBytes = decoder.Decode(baos.ToArray());
            Assert.That(buf.SequenceEqual(actualBytes), Is.True);
        }
	}
}
