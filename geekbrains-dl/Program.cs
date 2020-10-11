using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using System.Threading.Tasks;

namespace GeekBrainsDownloader
{
    class Program
    {
        private const string Dir = "./Courses/";

        static async Task Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine($"args.Length != 3");
                return;
            }

            GbDownloader gbDownloader = new GbDownloader(args[0], args[1], args[2]);
            await gbDownloader.Login();
            await gbDownloader.OpenCourse();
            await gbDownloader.GatherDataCourse();
            await gbDownloader.GetDataCourse();
        }
    }
}