using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.ADF;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using RoadGrinder.contracts;
using RoadGrinder.grinders;
using RoadGrinder.models;
using RoadGrinder.services;

namespace RoadGrinder
{
    internal class Program
    {
        private static readonly LicenseInitializer LicenseInitializer = new LicenseInitializer();

        private static void Main(string[] args)
        {
            CliOptions options;

            try
            {
                options = ArgParserService.Parse(args);
                if (options == null)
                {
                    return;
                }
            }
            catch (InvalidOperationException e)
            {
                Console.Write("road grinder: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("press any key to continue");
                Console.ReadKey();

                return;
            }

            const string roadsFeatureClassName = "Roads";
            var start = Stopwatch.StartNew();

            //ESRI License Initializer generated code
            //try to check out an arcinfo license
            if (!LicenseInitializer.InitializeApplication(new[] {esriLicenseProductCode.esriLicenseProductCodeAdvanced},
                new esriLicenseExtensionCode[] {}))
            {
                //if the license could not be initalized, shut it down
                Console.WriteLine(LicenseInitializer.LicenseMessage());
                Console.WriteLine("This application could not initialize with the correct ArcGIS license and will shutdown.");

                LicenseInitializer.ShutdownApplication();
                return;
            }
            Console.WriteLine("{0} Checked out a license", start.Elapsed);

            using (var releaser = new ComReleaser())
            {
                IWorkspace sgid;
                try
                {
                    Console.WriteLine("{1} Connecting to: {0}", options.SdeConnectionPath, start.Elapsed);

                    sgid = WorkspaceService.GetSdeWorkspace(options.SdeConnectionPath);
                    releaser.ManageLifetime(sgid);
                }
                catch (COMException e)
                {
                    Console.Write("road grinder: ");
                    Console.WriteLine(e.Message);

                    Console.ReadKey();
                    return;
                }

                var sgidFeatureWorkspace = (IFeatureWorkspace) sgid;
                releaser.ManageLifetime(sgidFeatureWorkspace);

                // get the source roads feature class (SGID)
                var roads = sgidFeatureWorkspace.OpenFeatureClass(roadsFeatureClassName);
                releaser.ManageLifetime(roads);

                IGrindable grinder;
                switch (options.OutputType)
                {
                    case OutputType.AlternateNames:
                    {
                        grinder = new AlternateNamesGrinder(roads, options);
                        break;
                    }
                    case OutputType.NextGen:
                    {
                        grinder = new NextGenGrinder();
                        break;
                    }
                    default:
                    {
                        return;
                    }
                }

                var output = grinder.CreateOutput();
                grinder.Grind(output);
            }
        }
    }
}
