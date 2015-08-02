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
using System.Collections.ObjectModel;
using NUnit.Framework;
using System.IO;

namespace hpack
{
	//@RunWith(Parameterized.class)
	public class HpackTest
	{
		//private static String TEST_DIR = "/hpack/";

		//private String fileName;

		public HpackTest(String fileName)
		{
			//this.fileName = fileName;
		}

		/*
		//@Parameters(name = "{0}")
		public static Collection<Object[]> data()
		{
			URL url = typeof(HpackTest).getResource(TEST_DIR);
			File[] files = new File(url.getFile()).listFiles();
			if (files == null) {
				throw new NullPointerException("files");
			}

			ArrayList<Object[]> data = new ArrayList<Object[]>();
			foreach(File file in files) {
				data.add(new Object[] { file.getName() });
			}
			return data;
		}

		[Test]
		public void test()
		{
			InputStream input = typeof(HpackTest).getResourceAsStream(TEST_DIR + fileName);
			TestCase testCase = TestCase.load(input);
			testCase.testCompress();
			testCase.testDecompress();
		}
		*/
	}
}
