﻿using LiteDB;
using LiteDB.Engine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteDB.Demo
{
    public class TestMemoryFile
    {
        static string PATH = @"E:\memory-file.db";
        static int N0 = 100;
        static int N1 = 10000;
        static BsonDocument doc = new BsonDocument
        {
            ["_id"] = 1,
            ["name"] = "NoSQL Database",
            ["birthday"] = new DateTime(1977, 10, 30),
            ["phones"] = new BsonArray { "000000", "12345678" },
            ["active"] = true
        }; // 109b

        public static void Run(Stopwatch sw)
        {
            File.Delete(PATH);
           
            var factory = new FileStreamDiskFactory(PATH, false);
            var file = new MemoryFile(factory, true);
            
            Console.WriteLine("Processing... " + (N0 * N1));
            
            sw.Start();
            
            // Write documents inside data file (append)
            WriteFile(file);
            
            Console.WriteLine("Write: " + sw.ElapsedMilliseconds);

            file.Dispose();
            file = new MemoryFile(factory, false);

            Thread.Sleep(2000);
            sw.Restart();

            ReadFile(file);

            Console.WriteLine("Read: " + sw.ElapsedMilliseconds);

            file.Dispose();

            //***********************
            var stream = new FileStream(PATH, FileMode.Open, FileAccess.Read, FileShare.Read, 8 * 8192, FileOptions.SequentialScan);

            sw.Restart();
            
            // Read document inside data file
            ReadFile2(stream);
            
            Console.WriteLine("Read Extend: " + sw.ElapsedMilliseconds);
            
            stream.Dispose();
        }

        static void ReadFile2(Stream stream)
        {
            var bytes = new byte[8 * 8192];
            var length = stream.Length;

            IEnumerable<ArraySlice<byte>> source()
            {
                var pos = 0;

                while (pos < length)
                {
                    stream.Read(bytes, 0, 8 * 8192);

                    var page = new ArraySlice<byte>(bytes, 0, 8 * 8192);

                    pos += (8 * 8192);

                    yield return page;
                }
            };

            using (var bufferReader = new BufferReader(source()))
            {
                for (var j = 0; j < N0 * N1; j++)
                {
                    var d = bufferReader.ReadDocument();
                }
            }
        }

        static void ReadFile(MemoryFile file)
        {
            var fileReader = file.GetReader(false);

            IEnumerable<ArraySlice<byte>> source()
            {
                var pos = 0;

                while (pos < file.Length)
                {
                    var page = fileReader.GetPage(pos);

                    pos += 8192;

                    yield return page;
                }
            };

            //for (var j = 0; j < N0; j++)
            //{
                using (var bufferReader = new BufferReader(source()))
                {
                    for (var i = 0; i < N0 * N1; i++)
                    {
                        var d = bufferReader.ReadDocument();
                    }
                }

                fileReader.ReleasePages();
            //}

            fileReader.Dispose();
        }

        static void WriteFile(MemoryFile file)
        {
            var fileReader = file.GetReader(true);

            var dirtyPages = new List<PageBuffer>();

            IEnumerable<ArraySlice<byte>> source()
            {
                while (true)
                {
                    var page = fileReader.NewPage();

                    dirtyPages.Add(page);

                    yield return page;
                }
            };

            //for (var j = 0; j < N0; j++)
            //{
                var bufferWriter = new BufferWriter(source());
                {
                    for (var i = 0; i < N0 * N1; i++)
                    {
                        doc["_id"] = i;

                        bufferWriter.WriteDocument(doc);
                    }
                }

                file.WriteAsync(dirtyPages);
                fileReader.ReleasePages();

                dirtyPages.Clear();

            //}

            // só posso fechar o reader apos ter enviado tudo para salvar (no caso as sujas)
            fileReader.Dispose();
        }
    }
}
