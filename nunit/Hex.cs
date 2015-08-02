/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
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
	/**
	 * Extracted from org/apache/commons/codec/binary/Hex.java
	 * Copyright Apache Software Foundation
	 */
	class Hex
	{
		/**
		 * Used to build output as Hex
		 */
		private static char[] DIGITS_LOWER = {
			'0',
			'1',
			'2',
			'3',
			'4',
			'5',
			'6',
			'7',
			'8',
			'9',
			'a',
			'b',
			'c',
			'd',
			'e',
			'f'
		};

		/**
		 * Used to build output as Hex
		 */
		private static char[] DIGITS_UPPER = {
			'0',
			'1',
			'2',
			'3',
			'4',
			'5',
			'6',
			'7',
			'8',
			'9',
			'A',
			'B',
			'C',
			'D',
			'E',
			'F'
		};

		/**
		 * Converts an array of characters representing hexadecimal values into an array of bytes of those same values. The
		 * returned array will be half the length of the passed array, as it takes two characters to represent any given
		 * byte. An exception is thrown if the passed char array has an odd number of elements.
		 *
		 * @param data
		 *            An array of characters containing hexadecimal digits
		 * @return A byte array containing binary data decoded from the supplied char array.
		 * @throws IOException
		 *             Thrown if an odd number or illegal of characters is supplied
		 */
		public static sbyte[] DecodeHex(char[] data)
		{
			int len = data.Length;

			if ((len & 0x01) != 0) {
				throw new IOException("Odd number of characters.");
			}

			sbyte[] output = new sbyte[len >> 1];

			// two characters form the hex value.
			for(int i = 0, j = 0; j < len; i++) {
				int f = Hex.ToDigit(data[j], j) << 4;
				j++;
				f = f | Hex.ToDigit(data[j], j);
				j++;
				output[i] = (sbyte)(f & 0xFF);
			}

			return output;
		}

		/**
		 * Converts an array of bytes into an array of characters representing the hexadecimal values of each byte in order.
		 * The returned array will be double the length of the passed array, as it takes two characters to represent any
		 * given byte.
		 *
		 * @param data
		 *            a byte[] to convert to Hex characters
		 * @return A char[] containing hexadecimal characters
		 */
		public static char[] EncodeHex(byte[] data)
		{
			return Hex.EncodeHex(data, true);
		}

		/**
		 * Converts an array of bytes into an array of characters representing the hexadecimal values of each byte in order.
		 * The returned array will be double the length of the passed array, as it takes two characters to represent any
		 * given byte.
		 *
		 * @param data
		 *            a byte[] to convert to Hex characters
		 * @param toLowerCase
		 *            <code>true</code> converts to lowercase, <code>false</code> to uppercase
		 * @return A char[] containing hexadecimal characters
		 * @since 1.4
		 */
		public static char[] EncodeHex(byte[] data, bool toLowerCase)
		{
			return Hex.EncodeHex(data, toLowerCase ? DIGITS_LOWER : DIGITS_UPPER);
		}

		/**
		 * Converts an array of bytes into an array of characters representing the hexadecimal values of each byte in order.
		 * The returned array will be double the length of the passed array, as it takes two characters to represent any
		 * given byte.
		 *
		 * @param data
		 *            a byte[] to convert to Hex characters
		 * @param toDigits
		 *            the output alphabet
		 * @return A char[] containing hexadecimal characters
		 * @since 1.4
		 */
		protected static char[] EncodeHex(byte[] data, char[] toDigits)
		{
			int l = data.Length;
			char[] output = new char[l << 1];
			// two characters form the hex value.
			for(int i = 0, j = 0; i < l; i++) {
				output[j++] = toDigits[(0xF0 & data[i]) >> 4];
				output[j++] = toDigits[0x0F & data[i]];
			}
			return output;
		}

		/**
		 * Converts an array of bytes into a String representing the hexadecimal values of each byte in order. The returned
		 * String will be double the length of the passed array, as it takes two characters to represent any given byte.
		 *
		 * @param data
		 *            a byte[] to convert to Hex characters
		 * @return A String containing hexadecimal characters
		 * @since 1.4
		 */
		public static String EncodeHexString(byte[] data)
		{
			return new String(Hex.EncodeHex(data));
		}

		/**
		 * Converts a hexadecimal character to an integer.
		 *
		 * @param ch
		 *            A character to convert to an integer digit
		 * @param index
		 *            The index of the character in the source
		 * @return An integer
		 * @throws IOException
		 *             Thrown if ch is an illegal hex character
		 */
		protected static int ToDigit(char ch, int index)
		{
			int digit = Convert.ToInt32(ch) - 48;
			if (digit >= 17 && digit <= 22) {
				digit -= 7;
			}
			if (digit >= 49 && digit <= 54) {
				digit -= 39;
			}
			if (digit < 0 || digit > 15) {
				throw new IOException("Illegal hexadecimal character " + ch + " at index " + index + "; digit: " + digit);
			}
			return digit;
		}
	}
}
