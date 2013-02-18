using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDeduper
{
    public class FileDeduperConsoleApp
    {
        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] left, byte[] right)
            {
                return left.SequenceEqual(right);
            }

            public int GetHashCode(byte[] key)
            {
                return key.Sum(b => b);
            }
        }

        private class DuplicateInfo
        {
            public readonly List<string> DuplicateFiles = new List<string>();
            public long FileSize;
        }

        /// <summary>
        /// Main
        /// </summary>
        /// <param name="args">Args</param>
        public static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: [extensions regex] [duplicate list file] pathToCheck1 pathToCheck2 etc...");
                return -1;
            }

            long duplicateSpace = 0;
            Regex re = new Regex(args[0], RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
            Dictionary<byte[], DuplicateInfo> dupes = new Dictionary<byte[], DuplicateInfo>(new ByteArrayComparer());
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            int count = 0;
            string outputFile = args[1];

            for (int i = 2; i < args.Length; i++)
            {
                try
                {
                    foreach (string d in Directory.EnumerateDirectories(args[i], "*", SearchOption.AllDirectories))
                    foreach (string f in Directory.EnumerateFiles(d, "*"))
                    {
                        string ext = Path.GetExtension(f);
                        if (re.IsMatch(f))
                        {
                            using (Stream stream = File.OpenRead(f))
                            {
                                // Console.WriteLine("Checking file {0} ({1} MB)", f, stream.Length / 1000000);
                                byte[] bytes = md5.ComputeHash(stream);
                                DuplicateInfo result;
                                if (!dupes.TryGetValue(bytes, out result))
                                {
                                    result = new DuplicateInfo { FileSize = stream.Length };
                                    dupes[bytes] = result;
                                }
                                result.DuplicateFiles.Add(f);

                                if (result.DuplicateFiles.Count != 1)
                                {
                                    duplicateSpace += stream.Length;
                                }
                            }
                        }

                        if (++count % 100 == 0)
                        {
                            Console.Write("Files checked: {0}\r", count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("*ERROR* {0}", ex.Message);
                }
            }

            Console.WriteLine("Files checked: {0}", count);

            using (StreamWriter writer = File.CreateText(outputFile))
            {
                writer.WriteLine("Files checked: {0}", count);
                writer.WriteLine("Duplicates: {0}", dupes.Values.Count(c => c.DuplicateFiles.Count != 1));
                writer.WriteLine("Duplicate space: {0} MB", duplicateSpace / 1000000);
                writer.WriteLine();

                foreach (var kv in dupes.Where(k => k.Value.DuplicateFiles.Count != 1).OrderByDescending(k => k.Value.FileSize))
                {
                    writer.WriteLine("File size: {0} MB", kv.Value.FileSize / 1000000);
                    foreach (string f in kv.Value.DuplicateFiles)
                    {
                        writer.WriteLine(f);
                    }
                    writer.WriteLine();
                }
            }

            return 0;
        }
    }
}
