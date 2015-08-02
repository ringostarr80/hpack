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
using System.Collections.Generic;
using NUnit.Framework;
using System.IO;

namespace hpack
{
	class TestCase
	{
		/*
		private static final Gson GSON = new GsonBuilder()
		.setFieldNamingPolicy(FieldNamingPolicy.LOWER_CASE_WITH_UNDERSCORES)
		.registerTypeAdapter(HeaderField.class, new HeaderFieldDeserializer())
		.create();

		int maxHeaderTableSize = -1;
		bool useIndexing = true;
		bool sensitiveHeaders;
		bool forceHuffmanOn;
		bool forceHuffmanOff;

		List<HeaderBlock> headerBlocks;

		private TestCase()
		{
			
		}

		static TestCase load(BinaryReader input)
		{
			InputStreamReader r = new InputStreamReader(input);
			TestCase testCase = GSON.fromJson(r, typeof(TestCase));
			for (HeaderBlock headerBlock : testCase.headerBlocks) {
				headerBlock.encodedBytes = Hex.decodeHex(headerBlock.getEncodedStr().toCharArray());
			}
			return testCase;
		}

		void testCompress()
		{
			Encoder encoder = createEncoder();

			for (HeaderBlock headerBlock : headerBlocks) {
				byte[] actual = encode(encoder, headerBlock.getHeaders(), headerBlock.getMaxHeaderTableSize(), sensitiveHeaders);

				if (!Arrays.equals(actual, headerBlock.encodedBytes)) {
					throw new AssertionError("\nEXPECTED:\n" + headerBlock.getEncodedStr() + "\nACTUAL:\n" + Hex.encodeHexString(actual));
				}

				List<HeaderField> actualDynamicTable = new ArrayList<HeaderField>();
				for (int index = 0; index < encoder.length(); index++) {
					actualDynamicTable.add(encoder.getHeaderField(index));
				}

				List<HeaderField> expectedDynamicTable = headerBlock.getDynamicTable();

				if (!expectedDynamicTable.equals(actualDynamicTable)) {
					throw new AssertionError("\nEXPECTED DYNAMIC TABLE:\n" + expectedDynamicTable + "\nACTUAL DYNAMIC TABLE:\n" + actualDynamicTable);
				}

				if (headerBlock.getTableSize() != encoder.size()) {
					throw new AssertionError("\nEXPECTED TABLE SIZE: " + headerBlock.getTableSize() + "\n ACTUAL TABLE SIZE : " + encoder.size());
				}
			}
		}

		void testDecompress()
		{
			Decoder decoder = createDecoder();

			for (HeaderBlock headerBlock : headerBlocks) {
				List<HeaderField> actualHeaders = decode(decoder, headerBlock.encodedBytes);

				List<HeaderField> expectedHeaders = new ArrayList<HeaderField>();
				foreach(HeaderField h in headerBlock.getHeaders()) {
					expectedHeaders.add(new HeaderField(h.name, h.value));
				}

				if (!expectedHeaders.equals(actualHeaders)) {
					throw new AssertionError("\nEXPECTED:\n" + expectedHeaders + "\nACTUAL:\n" + actualHeaders);
				}

				List<HeaderField> actualDynamicTable = new ArrayList<HeaderField>();
				for (int index = 0; index < decoder.length(); index++) {
					actualDynamicTable.add(decoder.getHeaderField(index));
				}

				List<HeaderField> expectedDynamicTable = headerBlock.getDynamicTable();

				if (!expectedDynamicTable.equals(actualDynamicTable)) {
					throw new AssertionError("\nEXPECTED DYNAMIC TABLE:\n" + expectedDynamicTable + "\nACTUAL DYNAMIC TABLE:\n" + actualDynamicTable);
				}

				if (headerBlock.getTableSize() != decoder.size()) {
					throw new AssertionError("\nEXPECTED TABLE SIZE: " + headerBlock.getTableSize() + "\n ACTUAL TABLE SIZE : " + decoder.size());
				}
			}
		}

		private Encoder createEncoder()
		{
			int maxHeaderTableSize = this.maxHeaderTableSize;
			if (maxHeaderTableSize == -1) {
				maxHeaderTableSize = Integer.MAX_VALUE;
			}

			return new Encoder(maxHeaderTableSize, useIndexing, forceHuffmanOn, forceHuffmanOff);
		}

		private Decoder createDecoder()
		{
			int maxHeaderTableSize = this.maxHeaderTableSize;
			if (maxHeaderTableSize == -1) {
				maxHeaderTableSize = Integer.MAX_VALUE;
			}

			return new Decoder(8192, maxHeaderTableSize);
		}

		private static byte[] encode(Encoder encoder, List<HeaderField> headers, int maxHeaderTableSize, boolean sensitive)
		{
			ByteArrayOutputStream baos = new ByteArrayOutputStream();

			if (maxHeaderTableSize != -1) {
				encoder.setMaxHeaderTableSize(baos, maxHeaderTableSize);
			}

			for (HeaderField e: headers) {
				encoder.encodeHeader(baos, e.name, e.value, sensitive);
			}

			return baos.toByteArray();
		}

		private static List<HeaderField> decode(Decoder decoder, byte[] expected)
		{
			List<HeaderField> headers = new ArrayList<HeaderField>();
			TestHeaderListener listener = new TestHeaderListener(headers);
			decoder.decode(new ByteArrayInputStream(expected), listener);
			decoder.endHeaderBlock();
			return headers;
		}

		private static String concat(List<String> l)
		{
			StringBuilder ret = new StringBuilder();
			for (String s : l) {
				ret.append(s);
			}
			return ret.toString();
		}

		static class HeaderBlock
		{
			private int maxHeaderTableSize = -1;
			private byte[] encodedBytes;
			private List<String> encoded;
			private List<HeaderField> headers;
			private List<HeaderField> dynamicTable;
			private int tableSize;

			private int getMaxHeaderTableSize()
			{
				return maxHeaderTableSize;
			}

			public String getEncodedStr()
			{
				return concat(encoded).replaceAll(" ", "");
			}

			public List<HeaderField> getHeaders()
			{
				return headers;
			}

			public List<HeaderField> getDynamicTable()
			{
				return dynamicTable;
			}

			public int getTableSize()
			{
				return tableSize;
			}
		}

		static class HeaderFieldDeserializer implements JsonDeserializer<HeaderField>
		{
			//@Override
			public HeaderField deserialize(JsonElement json, Type typeOfT, JsonDeserializationContext context)
			{
				JsonObject jsonObject = json.getAsJsonObject();
				Set<Map.Entry<String, JsonElement>> entrySet = jsonObject.entrySet();
				if (entrySet.size() != 1) {
					throw new JsonParseException("JSON Object has multiple entries: " + entrySet);
				}
				Map.Entry<String, JsonElement> entry = entrySet.iterator().next();
				String name = entry.getKey();
				String value = entry.getValue().getAsString();
				return new HeaderField(name, value);
			}
		}
		*/
	}
}
