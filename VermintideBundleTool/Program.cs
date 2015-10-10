﻿using System;
using System.Collections.Generic;
using System.Data.HashFunction;
using System.Globalization;
using System.IO;
using System.Text;
using VermintideBundleTool.Bundle;

namespace VermintideBundleTool
{
    class Program
    {
        static void Main(string[] args)
        {

            Dictionary<ulong, string> nameDictionary = new Dictionary<ulong, string>();
            var murmur = new MurmurHash2();
            foreach (string fileName in File.ReadAllLines("dictionary.txt"))
            {
                ulong hash = BitConverter.ToUInt64(murmur.ComputeHash(Encoding.ASCII.GetBytes(fileName)), 0);
                nameDictionary[hash] = fileName;
            }

            var extactFile = @"E:\Games\Steam_Library\SteamApps\common\Warhammer End Times Vermintide Public Test\bundle\renamed\resource_packages\ingame";
            ExtractFilesFromBundleFile(extactFile, nameDictionary);


            //var bundlePath = @"E:\Games\Steam_Library\SteamApps\common\Warhammer End Times Vermintide Public Test\bundle";
            //var outputDir = @"E:\Games\Steam_Library\SteamApps\common\Warhammer End Times Vermintide Public Test\bundle\renamed";
            //RenameBundleFiles(bundlePath, nameDictionary, outputDir);

            //var databasePath = @"E:\Games\Steam_Library\SteamApps\common\Warhammer End Times Vermintide Public Test\bundle\bundle_database.data";
            //ReadBundleDatabase(databasePath);
        }

        private static void ReadBundleDatabase(string databasePath)
        {
            using (var input = File.OpenRead(databasePath))
            {
                var database = new BundleDatabaseFile();
                database.Read(input);
            }
        }

        private static void RenameBundleFiles(string bundlePath, Dictionary<ulong, string> nameDictionary, string outputDir)
        {
            foreach (var filePath in Directory.GetFiles(bundlePath))
            {
                var fileName = Path.GetFileName(filePath);
                var extension = Path.GetExtension(filePath);

                ulong hash;
                if (ulong.TryParse(fileName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash))
                {
                    string trueName;
                    if (nameDictionary.TryGetValue(hash, out trueName))
                    {
                        string destName = trueName + extension;
                        var outputFilePath = Path.Combine(outputDir, destName);
                        var outputDirectory = Path.GetDirectoryName(outputFilePath);
                        if (!Directory.Exists(outputDirectory))
                        {
                            Directory.CreateDirectory(outputDirectory);
                        }

                        File.Copy(filePath, outputFilePath);
                    }
                }
            }
        }

        private static void ExtractFilesFromBundleFile(string extactFile, Dictionary<ulong, string> fileNames)
        {
            using (var input = File.OpenRead(extactFile))
            {
                BundleFile file = new BundleFile();
                file.Read(input);

                string outPath = Path.Combine(Path.GetDirectoryName(extactFile) + "\\" + Path.GetFileName(extactFile) + "_out\\");
                Directory.CreateDirectory(outPath);

                foreach (var entry in file.Entries)
                {
                    string entryExtension;
                    string entryName;
                    if (!fileNames.TryGetValue(entry.Hash.ExtensionHash, out entryExtension))
                    {
                        entryExtension = entry.Hash.ExtensionHash.ToString("x");
                    }

                    if (!fileNames.TryGetValue(entry.Hash.NameHash, out entryName))
                    {
                        entryName = entry.Hash.NameHash.ToString("x");
                    }

                    string entryFileName = entryName + "." + entryExtension;
                    string outFilePath = Path.Combine(outPath, entryFileName);

                    var directoryName = Path.GetDirectoryName(outFilePath);
                    if (!Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    File.WriteAllBytes(outFilePath, entry.Data);

                    entry.Data = null;
                }
            }
        }
    }
}