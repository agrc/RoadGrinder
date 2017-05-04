using System;
using System.IO;
using Mono.Options;
using RoadGrinder.models;

namespace RoadGrinder.services
{
    internal static class ArgParserService
    {
        internal static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            var showHelp = false;

            var p = new OptionSet
            {
                {
                    "c|connection=", "REQUIRED. the path to the .sde connection file for the database containing roads. eg: c:\\sgid.sde",
                    v => options.SdeConnectionPath = v
                },
                {
                    "t|type=", "REQUIRED. The output type you want to grind. `AlternateNames`, `NextGen`, ...",
                    v => options.OutputType = (OutputType) Enum.Parse(typeof (OutputType), v)
                },
                {
                    "o|output=",
                    "the location to save the output of this tool. eg: c:\\locators. Defaults to current working directory.",
                    v => options.OutputGeodatabase = v
                },
                {
                    "v", "increase the debug message verbosity.",
                    v =>
                    {
                        if (v != null)
                        {
                            options.Verbose = true;
                        }
                    }
                },
                {
                    "h|help", "show this message and exit",
                    v => showHelp = v != null
                }
            };

            try
            {
                p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("Road Grinder: ");
                Console.WriteLine(e.Message);
                ShowHelp(p);

                return null;
            }

            if (showHelp)
            {
                ShowHelp(p);

                return null;
            }

            if (string.IsNullOrEmpty(options.SdeConnectionPath))
            {
                throw new InvalidOperationException(
                    "Missing required option -c for the location of the sde connection file.");
            }

            if (!new FileInfo(options.SdeConnectionPath).Exists)
            {
                var cwd = Directory.GetCurrentDirectory();
                var location = Path.Combine(cwd, options.SdeConnectionPath.TrimStart('\\'));

                if (!new FileInfo(location).Exists)
                {
                    throw new InvalidOperationException("The location for the sde file path is not found.");
                }

                options.SdeConnectionPath = location;
            }

            if (showHelp)
            {
                ShowHelp(p);
            }

            return options;
        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: road grinder [OPTIONS]+");
            Console.WriteLine();
            Console.WriteLine("Options:");

            p.WriteOptionDescriptions(Console.Out);
        }
    }
}