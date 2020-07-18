//#define PROCESS_ATTACH_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Vts.MonteCarlo.PostProcessor
{
    #region CommandLine Arguments Parser

    /* Simple commandline argument parser written by Ananth B. http://www.ananthonline.net */
    static class CommandLine
    {
        public class Switch // Class that encapsulates switch data.
        {
            public Switch(string name, string shortForm, Action<IEnumerable<string>> handler)
            {
                Name = name;
                ShortForm = shortForm;
                Handler = handler;
            }

            public Switch(string name, Action<IEnumerable<string>> handler)
            {
                Name = name;
                ShortForm = null;
                Handler = handler;
            }

            public string Name { get; private set; }
            public string ShortForm { get; private set; }
            public Action<IEnumerable<string>> Handler { get; private set; }

            public int InvokeHandler(string[] values)
            {
                Handler(values);
                return 1;
            }
        }

        /* The regex that extracts names and comma-separated values for switches 
        in the form (<switch>[="value 1",value2,...])+ */
        private static readonly Regex ArgRegex =
            new Regex(@"(?<name>[^=]+)=?((?<quoted>\""?)(?<value>(?(quoted)[^\""]+|[^,]+))\""?,?)*",
                RegexOptions.Compiled | RegexOptions.CultureInvariant |
                RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        private const string NameGroup = "name"; // Names of capture groups
        private const string ValueGroup = "value";

        public static void Process(this string[] args, Action printUsage, params Switch[] switches)
        {
            /* Run through all matches in the argument list and if any of the switches 
            match, get the values and invoke the handler we were given. We do a Sum() 
            here for 2 reasons; a) To actually run the handlers
            and b) see if any were invoked at all (each returns 1 if invoked).
            If none were invoked, we simply invoke the printUsage handler. */
            if ((from arg in args
                 from Match match in ArgRegex.Matches(arg)
                 from s in switches
                 where match.Success &&
                     ((string.Compare(match.Groups[NameGroup].Value, s.Name, true) == 0) ||
                     (string.Compare(match.Groups[NameGroup].Value, s.ShortForm, true) == 0))
                 select s.InvokeHandler(match.Groups[ValueGroup].Value.Split(','))).Sum() == 0)
                printUsage(); // We didn't find any switches
        }
    }

    #endregion

    public static class Program
    {
        /// <summary>
        /// main Monte Carlo Post Processor (MCPP) application
        /// </summary>
        /// <returns>int = 0 (successful completion)</returns>
        /// <returns>int = 1 (infile null or missing)</returns>
        /// <returns>int = 2 (infile exists but does not pass validation)</returns>
        public static int Main(string[] args)
        {
#if PROCESS_ATTACH_DEBUG
            Console.Read();
#endif
            string inFile = "infile.txt";
            string inPath = "";
            string outName = "";
            string outPath = "";
            bool infoOnlyOption = false;
            args.Process(() =>
                {
                    Console.WriteLine("Virtual Photonics MC Post-Processor 1.0");
                    Console.WriteLine();
                    Console.WriteLine("For more information type mc_post help");
                    Console.WriteLine();
                },
                new CommandLine.Switch("help", val =>
                {
                    infoOnlyOption = true;
                    ShowHelp();
                    return;
                }),
                new CommandLine.Switch("geninfiles", val =>
                {
                    GenerateDefaultInputFiles();
                    infoOnlyOption = true;
                    return;
                }),
                new CommandLine.Switch("infile", val =>
                {
                    inFile = val.First();
                    Console.WriteLine("input file specified as {0}", inFile);
                    //PostProcessorSetup.InputFilename = val.First();
                }),
                new CommandLine.Switch("inpath", val =>
                {
                    inPath = val.First();
                    Console.WriteLine("input path specified as {0}", inPath);
                }),
                new CommandLine.Switch("outname", val =>
                {
                    outName = val.First();
                    Console.WriteLine("output file specified as {0}", outName);
                    //PostProcessorSetup.OutputFolder = val.First();
                }),
                new CommandLine.Switch("outpath", val =>
                {
                    outPath = val.First();
                    Console.WriteLine("output path specified as {0}", outPath);
                })
            );

            if (!infoOnlyOption)
            {
                var input = PostProcessorSetup.ReadPostProcessorInputFromFile(inFile);
                if (input == null)
                {
                    return 1;
                }

                var validationResult = PostProcessorSetup.ValidatePostProcessorInput(input, inPath);
                if (!validationResult.IsValid)
                {
                    Console.Write("\nPost-processor) completed with errors. Press enter key to exit.");
                    Console.Read();
                    return 2;
                }
                // override the output name with the user-specified name
                if (!string.IsNullOrEmpty(outName))
                {
                    input.OutputName = outName;
                }
                PostProcessorSetup.RunPostProcessor(input, inPath, outPath);
                Console.WriteLine("\nPost-processing complete.");
                return 0;
            }
            return 0;
        }

        private static void GenerateDefaultInputFiles()
        {
            var infiles = PostProcessorInputProvider.GenerateAllPostProcessorInputs();
            for (int i = 0; i < infiles.Count; i++)
            {
                infiles[i].ToFile("infile_" + infiles[i].OutputName + ".txt"); 
            }

        }

        /// <summary>
        /// Displays the help text for detailed usage of the application
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("Virtual Photonics MC Post-Processor 1.0");
            Console.WriteLine();
            Console.WriteLine("list of arguments:");
            Console.WriteLine();
            Console.WriteLine("infile\t\tthe input file, accepts relative and absolute paths");
            Console.WriteLine("inpath\t\tthe input path, accepts relative and absolute paths");
            Console.WriteLine("outpath\t\tthe output path, accepts relative and absolute paths");
            Console.WriteLine("outname\t\toutput name, this overwrites output name in input file");
            Console.WriteLine();
            Console.WriteLine("geninfiles\t\tgenerates example infiles and names them infile_XXX.txt");
            Console.WriteLine();
            Console.WriteLine("sample usage:");
            Console.WriteLine();
            Console.WriteLine("mc_post infile=myinput outname=myoutput");
        }
    }
}


