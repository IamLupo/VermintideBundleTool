﻿using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using VermintideBundleTool.Bundle;

namespace VermintideBundleTool
{
    class Program
    {
        private const string DictionaryFilePath = "dictionary.txt";

        [Verb("unpack", HelpText = "Unpack a bundle file.")]
        class UnpackOption
        {
            [Option('i', "input", Required = true,
                Min = 1,
                HelpText = "Input files to be processed.")]
            public IEnumerable<string> InputFiles { get; set; }

            [Option('o', "output",
                HelpText = "Output directory")]
            public string OutputDirectory { get; set; }

            [Option(HelpText = "Prints all messages to standard output.")]
            public bool Verbose { get; set; }
        }

        [Verb("rename", HelpText = "Rename bundle files in a directory.")]
        class RenameOption
        {
            [Option('i', "input", Required = true,
                HelpText = "Input directory.")]

            public string InputDirectory { get; set; }

            [Option('o', "output",
                HelpText = "Output directory for renamed files.")]
            public string OutputDirectory { get; set; }

            [Option(HelpText = "Prints all messages to standard output.")]
            public bool Verbose { get; set; }
        }

        static int Main(string[] args)
        {
            var errorCode = CommandLine.Parser.Default.ParseArguments<UnpackOption, RenameOption>(args)
                .MapResult(
                    (UnpackOption opts) => RunUnpackAndReturnExitCode(opts),
                    (RenameOption opts) => RunRenameAndReturnExitCode(opts),
                    errs => 1);
            return errorCode;
        }

        private static int RunUnpackDirectory(string bundleDirectory, string outputDirectory)
        {
            var files = Directory.GetFiles(
                bundleDirectory, "*",
                SearchOption.AllDirectories)
                .GroupBy(Path.GetExtension)
                .Where(g => g.Key == "" || g.Key == ".patch_0")
                .OrderBy(f => f.Key)
                .SelectMany(f => f)
                .ToList();
            foreach (var file in files)
            {
                try
                {
                    RunUnpackAndReturnExitCode(new UnpackOption
                    {
                        InputFiles = new string[] {file},
                        OutputDirectory = outputDirectory,
                        Verbose = false
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error unpacking '{0}': {1}", file, ex);
                }
            }
            return 0;
        }

        private static int RunUnpackAndReturnExitCode(UnpackOption opts)
        {
            var nameDictionary = ReadDictionary(DictionaryFilePath);

            var inputFilePaths = opts.InputFiles.ToArray();
            int i = 1;
            foreach (var inputFilePath in inputFilePaths)
            {
                var fileName = Path.GetFileName(inputFilePath);
                WriteLine(
                    string.Format("Unpacking file {0}/{1} '{2}'", i, inputFilePaths.Length, fileName),
                    opts.Verbose);
                string outPath;
                if (string.IsNullOrEmpty(opts.OutputDirectory))
                {
                    outPath = Path.Combine(Path.GetDirectoryName(inputFilePath), fileName + "_out\\");
                }
                else
                {
                    outPath = Path.Combine(opts.OutputDirectory, fileName);
                }

                ExtractFilesFromBundleFile(inputFilePath, outPath, nameDictionary, opts.Verbose);

                WriteLine(
                    string.Format("Unpacked file '{0}'", fileName),
                    opts.Verbose);
                i++;
            }
            return 0;
        }

        private static int RunRenameAndReturnExitCode(RenameOption opts)
        {
            var nameDictionary = ReadDictionary(DictionaryFilePath);

            if (string.IsNullOrEmpty(opts.OutputDirectory))
            {
                opts.OutputDirectory = opts.InputDirectory + "_renamed";
            }

            if (!Directory.Exists(opts.OutputDirectory))
            {
                Directory.CreateDirectory(opts.OutputDirectory);
            }

            var inputFilePaths = Directory.GetFiles(opts.InputDirectory);
            int i = 1;
            foreach (var inputFilePath in inputFilePaths)
            {
                var fileName = Path.GetFileNameWithoutExtension(inputFilePath);
                var extension = Path.GetExtension(inputFilePath);
                WriteLine(
                    string.Format("Renaming file {0}/{1}", i, inputFilePaths.Length),
                    opts.Verbose);

                ulong hash;
                string name;
                if (ulong.TryParse(fileName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash)
                    && nameDictionary.TryGetValue(hash, out name))
                {
                    string outputFileName = name + extension;

                    WriteLine(
                        string.Format("Renamed {0} to {1}", fileName, outputFileName),
                        opts.Verbose);

                    var outputFilePath = Path.Combine(opts.OutputDirectory, outputFileName);
                    var outputDirectory = Path.GetDirectoryName(outputFilePath);
                    if (!Directory.Exists(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    File.Copy(inputFilePath, outputFilePath, true);
                }
                else
                {
                    string outputFileName = fileName + extension;

                    Debug.WriteLine(string.Format("Missing name '{0}'", outputFileName));
                    WriteLine(
                        string.Format("Unable to renamed {0}. Unknown ID", outputFileName),
                        opts.Verbose);
                    var outputFilePath = Path.Combine(opts.OutputDirectory, outputFileName);
                    File.Copy(inputFilePath, outputFilePath, true);
                }
                i++;
            }
            return 0;
        }

        private static Dictionary<ulong, string> ReadDictionary(string dictionaryFilePath)
        {
            Dictionary<ulong, string> nameDictionary = new Dictionary<ulong, string>();
            var murmur = new MurmurHash2();
            foreach (string fileName in File.ReadAllLines(dictionaryFilePath))
            {
                ulong hash = BitConverter.ToUInt64(murmur.ComputeHash(Encoding.ASCII.GetBytes(fileName)), 0);
                string existingFileName;
                if (nameDictionary.TryGetValue(hash, out existingFileName))
                {
                    Debug.WriteLine("Overwriting '" + hash.ToString("x") +"' '" + existingFileName + "' with '" + fileName + "'");
                }

                nameDictionary[hash] = fileName;
            }
            return nameDictionary;
        }

        private static void ExtractFilesFromBundleFile(string extactFile, string outPath, Dictionary<ulong, string> fileNames, bool verbose = false)
        {
            using (var input = File.OpenRead(extactFile))
            using (var temporaryFile = File.Create(Path.GetTempFileName(), 4 * 1024, FileOptions.DeleteOnClose))
            {
                WriteLine("Reading bundle", verbose);
                BundleFile file = new BundleFile();
                file.Read(input, temporaryFile);

                WriteLine(string.Format("Found {0} files in bundle", file.Entries.Count), verbose);
                int i = 1;
                foreach (var entry in file.Entries)
                {
                    string entryExtension;
                    string entryName;
                    if (!fileNames.TryGetValue(entry.ExtensionHash, out entryExtension))
                    {
                        entryExtension = entry.ExtensionHash.ToString("x");
                        Debug.WriteLine(string.Format("Missing extension '{0}'", entryExtension));
                    }

                    if (!fileNames.TryGetValue(entry.NameHash, out entryName))
                    {
                        entryName = entry.NameHash.ToString("x");
                        Debug.WriteLine(string.Format("Missing name '{0}'", entryName));
                    }

                    string entryFileName = entryName + "." + entryExtension;
                    string outFilePath = Path.Combine(outPath, entryFileName);

                    var directoryName = Path.GetDirectoryName(Path.Combine(outPath, entryFileName));
                    if (!Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    
                    foreach (var data in entry.ReadData(temporaryFile))
                    {
                        entryFileName = entryName + ToLanguageName(data.LanguageId) + "." + entryExtension;
                        outFilePath = Path.Combine(outPath, entryFileName);
                        WriteLine(string.Format("Extracting file {0}/{1} '{2}'", i, file.Entries.Count, entryFileName), verbose);

                        File.WriteAllBytes(outFilePath, data.Data);
                    }

                    i++;
                }
            }
        }


        private static string ToLanguageName(int languageId)
        {
            switch (languageId)
            {
                case 0:
                    return ""; // eng
                case 1:
                    return ".ger";
                case 2:
                    return ".spa";
                case 4:
                    return ".rus";
                case 8:
                    return ".fre";
                default:
                    return ".unknown_" + languageId; // 32, 64, 128
            }
        }

        private static void WriteLine(string text, bool verbose, bool verboseRequired = true)
        {
            if (!verboseRequired || verbose)
            {
                Console.WriteLine(text);
            }
        }

        private static void ReadBundleDatabase(string databasePath)
        {
            using (var input = File.OpenRead(databasePath))
            {
                var database = new BundleDatabaseFile();
                database.Read(input);
            }
        }
    }
}
