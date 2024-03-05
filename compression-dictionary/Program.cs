using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

class Program
{
	static void Main()
	{
		string inputFilePath = "../../../file_output.txt";
		string outputFilePath = "../../../output.bin";

		Dictionary<string, List<int>> invertedIndex = ReadInvertedIndex(inputFilePath);

		List<string> words = invertedIndex.Keys.Select(key => key).ToList();

		WriteWordsToFile(words, "../../../words.txt");

		Stopwatch stopwatchCompress = Stopwatch.StartNew();
		string compressedData = CompressFrontCoding(words);
		WriteCompresedDataToFile(compressedData, "../../../compressedData.txt");
		stopwatchCompress.Stop();
		Console.WriteLine($"CompressFrontCoding executed in {stopwatchCompress.ElapsedMilliseconds} ms.");


		Stopwatch stopwatchDecompress = Stopwatch.StartNew();
		List<string> decompressedWords = DecompressFrontCoding(compressedData);
		WriteWordsToFile(decompressedWords, "../../../decompressedWords.txt");
		stopwatchDecompress.Stop();
		Console.WriteLine($"DecompressFrontCoding executed in {stopwatchDecompress.ElapsedMilliseconds} ms.");



		Stopwatch stopwatchEncodeVBC = Stopwatch.StartNew();
		EncodeAndWriteVBC(invertedIndex, outputFilePath);
		stopwatchEncodeVBC.Stop();
		Console.WriteLine($"EncodeAndWriteVBC executed in {stopwatchEncodeVBC.ElapsedMilliseconds} ms.");

		string binaryFilePath = "../../../output.bin";
		string outputTextFilePath = "../../../output.txt";

		Stopwatch stopwatchDecodeVBC = Stopwatch.StartNew();
		Dictionary<string, List<int>> invertedIndexDecoded = DecodeVBC(binaryFilePath);
		stopwatchDecodeVBC.Stop();
		Console.WriteLine($"DecodeVBC executed in {stopwatchDecodeVBC.ElapsedMilliseconds} ms.");

		WriteInvertedIndex(invertedIndexDecoded, outputTextFilePath);

	}

	private static void WriteCompresedDataToFile(string compressedData, string v)
	{
		using (StreamWriter writer = new StreamWriter(v))
		{
			writer.WriteLine(compressedData);
		}
	}

	private static void WriteWordsToFile(List<string> words, string v)
	{
		using (StreamWriter writer = new StreamWriter(v))
		{
			foreach (var word in words)
			{
				writer.WriteLine(word);
			}
		}
	}

	static Dictionary<string, List<int>> ReadInvertedIndex(string filePath)
	{
		Dictionary<string, List<int>> invertedIndex = new Dictionary<string, List<int>>();

		using (StreamReader reader = new StreamReader(filePath))
		{
			while (!reader.EndOfStream)
			{
				string line = reader.ReadLine();
				string[] parts = line.Split(':');

				if (parts.Length == 2)
				{
					string term = parts[0].Trim();
					string[] docIds = parts[1].Split(',');

					if (!invertedIndex.ContainsKey(term))
					{
						invertedIndex[term] = new List<int>();
					}

					foreach (string docId in docIds)
					{
						if (int.TryParse(docId, out int id))
						{
							invertedIndex[term].Add(id);
						}
					}
				}
			}
		}

		return invertedIndex;
	}

	static void EncodeAndWriteVBC(Dictionary<string, List<int>> invertedIndex, string outputFilePath)
	{
		using (BinaryWriter writer = new BinaryWriter(File.Open(outputFilePath, FileMode.Create)))
		{
			foreach (var entry in invertedIndex)
			{
				byte[] termBytes = Encoding.UTF8.GetBytes(entry.Key);
				writer.Write(termBytes.Length);
				writer.Write(termBytes);

				List<int> docIds = entry.Value;
				List<byte> vbcBytes = new List<byte>();

				writer.Write(docIds.Count);

				foreach (int docId in docIds)
				{
					vbcBytes.AddRange(VariableByteEncode(docId));
				}

				writer.Write(vbcBytes.ToArray());
			}
		}
	}

	static List<byte> VariableByteEncode(int value)
	{
		List<byte> bytes = new List<byte>();

		do
		{
			byte lower7bits = (byte)(value & 0x7F);
			value >>= 7;

			if (value > 0)
			{
				lower7bits |= 0x80;
			}

			bytes.Add(lower7bits);
		} while (value > 0);

		return bytes;
	}

	static Dictionary<string, List<int>> DecodeVBC(string filePath)
	{
		Dictionary<string, List<int>> invertedIndex = new Dictionary<string, List<int>>();

		using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
		using (BinaryReader reader = new BinaryReader(fs))
		{
			byte[] buffer = new byte[fs.Length];

			while (true)
			{
				int bytesRead = reader.Read(buffer, 0, buffer.Length);

				if (bytesRead == 0)
				{
					break;
				}

				int position = 0;

				while (position < bytesRead)
				{
					int termLength = BitConverter.ToInt32(buffer, position);
					position += sizeof(int);

					string term = Encoding.UTF8.GetString(buffer, position, termLength);
					position += termLength;

					int numIndices = BitConverter.ToInt32(buffer, position);
					position += sizeof(int);

					List<int> docIds = new List<int>();
					for (int i = 0; i < numIndices; i++)
					{
						int docId = VariableByteDecode(buffer, ref position);
						docIds.Add(docId);
					}

					invertedIndex[term] = docIds;
				}
			}
		}

		return invertedIndex;
	}

	static int VariableByteDecode(byte[] buffer, ref int position)
	{
		int result = 0;
		int shift = 0;

		byte currentByte;
		do
		{
			currentByte = buffer[position++];
			result |= (currentByte & 0x7F) << shift;
			shift += 7;
		} while ((currentByte & 0x80) != 0);

		return result;
	}


	static void WriteInvertedIndex(Dictionary<string, List<int>> invertedIndex, string outputFilePath)
	{
		using (StreamWriter writer = new StreamWriter(outputFilePath))
		{
			foreach (var entry in invertedIndex)
			{
				string term = entry.Key;
				List<int> docIds = entry.Value;

				writer.Write($"{term}: ");
				writer.WriteLine(string.Join(", ", docIds));
			}
		}
	}

	static string CompressFrontCoding(List<string> words)
	{
		StringBuilder compressedData = new StringBuilder();

		for (int i = 0; i < words.Count; i += 4)
		{
			int blockSize = Math.Min(4, words.Count - i);
			List<string> block = words.GetRange(i, blockSize);

			int commonPrefixLength = GetCommonPrefixLength(block);
			compressedData.Append(commonPrefixLength.ToString() + block[0].Substring(0, commonPrefixLength));

			for (int j = 0; j < blockSize; j++)
			{
				string remainingPart = block[j].Substring(commonPrefixLength);
				compressedData.Append(remainingPart.Length + remainingPart);
			}
		}

		return compressedData.ToString();
	}

	static List<string> DecompressFrontCoding(string compressedData)
	{
		List<string> decompressedWords = new List<string>();
		for(int i = 0; i < compressedData.Length;)
		{
			int commonPrefixLength;
			if (compressedData[i] == '0')
			{
				commonPrefixLength = 0;
				i++;
			}
			else if (int.TryParse(compressedData[i].ToString() + compressedData[i+1].ToString(), out int commonPrefixLengthTwoDigits))
			{
				i += 2;
				commonPrefixLength = commonPrefixLengthTwoDigits;
			} else 
			{
				commonPrefixLength = int.Parse(compressedData[i].ToString());
				i++;
			}
			
			string commonPrefix = compressedData.Substring(i, commonPrefixLength);
			i += commonPrefixLength;

			for (int j = 0; j < 4; j++)
			{
				if (i < compressedData.Length)
				{
					if (compressedData[i] == '0')
					{
						i++;
						decompressedWords.Add(commonPrefix);
						continue;
					}
					if (int.TryParse(compressedData[i].ToString() + compressedData[i+1].ToString(), out int remainingPartTwoDigits))
					{
						i += 2;
						string remainingPart = compressedData.Substring(i, remainingPartTwoDigits);
						i += remainingPartTwoDigits;
						decompressedWords.Add(commonPrefix + remainingPart);
					} else
					{
						int remainingPartLength = int.Parse(compressedData[i].ToString());
						i++;

						string remainingPart = compressedData.Substring(i, remainingPartLength);
						i += remainingPartLength;

						decompressedWords.Add(commonPrefix + remainingPart);
					}
					
				}
			}
		}

		return decompressedWords;		
	}

	static int GetCommonPrefixLength(List<string> words)
	{
		int commonPrefixLength = 0;
		bool commonPrefix = true;

		while (commonPrefix)
		{
			if (words.Count > 1)
			{
				if (commonPrefixLength >= words[0].Length)
				{
					return commonPrefixLength;
				}
				char currentChar = words[0][commonPrefixLength];

				for (int i = 1; i < words.Count; i++)
				{
					if (commonPrefixLength >= words[i].Length || words[i][commonPrefixLength] != currentChar)
					{
						commonPrefix = false;
						break;
					}
				}
			}
			else
			{
				commonPrefix = false;
			}

			if (commonPrefix)
			{
				commonPrefixLength++;
			}
		}

		return commonPrefixLength;
	}

}
