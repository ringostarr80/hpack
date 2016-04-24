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
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace hpack
{
	public class HuffmanTest
	{
		[Test]
		public void testHuffman()
		{
			var s = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			for(var i = 0; i < s.Length; i++) {
				roundTrip(s.Substring(0, i));
			}

			var random = new Random(123456789);
			var buf = new byte[4096];
			random.NextBytes(buf);
			roundTrip(buf);
		}

		[Test]
		public void testDecodeEOS()
		{
			var buf = new byte[4];
			for(var i = 0; i < 4; i++) {
				buf[i] = (byte)0xFF;
			}
			Assert.Throws<IOException>(delegate { Huffman.DECODER.Decode(buf); });
		}

		[Test]
		public void testDecodeIllegalPadding()
		{
			var buf = new byte[1];
			buf[0] = 0x00; // '0', invalid padding
			Assert.Throws<IOException>(delegate { Huffman.DECODER.Decode(buf); });
		}

		[Test]
		public void testDecodeExtraPadding()
		{
			var buf = new byte[2];
			buf[0] = 0x0F; // '1', 'EOS'
			buf[1] = (byte)0xFF; // 'EOS'
			Huffman.DECODER.Decode(buf);
		}

		private void roundTrip(String s)
		{
			roundTrip(Huffman.ENCODER, Huffman.DECODER, s);
		}

		private static void roundTrip(HuffmanEncoder encoder, HuffmanDecoder decoder, string s)
		{
			roundTrip(encoder, decoder, Encoding.UTF8.GetBytes(s));
		}

		private void roundTrip(byte[] buf)
		{
			roundTrip(Huffman.ENCODER, Huffman.DECODER, buf);
		}

		private static void roundTrip(HuffmanEncoder encoder, HuffmanDecoder decoder, byte[] buf)
		{
			using(var baos = new MemoryStream()) {
				using(var dos = new BinaryWriter(baos)) {
					encoder.Encode(dos, buf);
					var actualBytes = decoder.Decode(baos.ToArray());
					Assert.IsTrue(buf.SequenceEqual(actualBytes));
				}
			}
		}
	}
}
