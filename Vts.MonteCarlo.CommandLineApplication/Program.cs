//#define PROCESS_ATTACH_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NLog;
using Vts.Common.Logging;

namespace Vts.MonteCarlo.CommandLineApplication
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
    /// <summary>
    /// Monte Carlo command line application program.  Type "mc help" for
    /// a description of the different command line parameters.
    /// </summary>
    public static class Program
    {
        private static Common.Logging.ILogger logger = LoggerFactoryLocator.GetDefaultNLogFactory().Create(typeof(Program));

        /// <summary>
        /// main Monte Carlo CommandLine (MCCL) application
        /// </summary>
        /// <param name="args"></param>
        /// <returns>int = 0 (successful completion)</returns>
        /// <returns>int = 1 (infile null or missing)</returns>
        /// <returns>int = 2 (infile exists but does not pass validation)</returns>
        public static int Main(string[] args)
        {
#if PROCESS_ATTACH_DEBUG
            Console.Read();
#endif
            var inFile = "";
            var inFiles = new List<string>();
            var outName = "";
            var outPath = "";
            var CPUCount = "1"; // default is to use 1
            var infoOnlyOption = false;
            IList<ParameterSweep> paramSweep = new List<ParameterSweep>();

            args.Process(() =>
               {
                   logger.Info($"\nVirtual Photonics MC {GetVersionNumber(3)}\n");
                   logger.Info("For more information type mc help");
                   logger.Info("For help on a specific topic type dotnet mc.dll help=<topicname>\n");
            },
               new CommandLine.Switch("help", val =>
               {
                   var helpTopic = val.First();
                   if (helpTopic != "")
                       ShowHelp(helpTopic);
                   else
                       ShowHelp();
                   infoOnlyOption = true;
               }),
               new CommandLine.Switch("geninfiles", val =>
               {
                   GenerateDefaultInputFiles();
                   infoOnlyOption = true;
               }),
               new CommandLine.Switch("infile", val =>
               {
                   inFile = val.First();
                   logger.Info(() => "input file specified as " + inFile);
               }),
               new CommandLine.Switch("infiles", val =>
               {
                   inFiles.AddRange(val);
                   foreach (var file in inFiles)
                   {
                       logger.Info(() => "input file specified as " + file);
                   }
               }),
               new CommandLine.Switch("outname", val =>
               {
                   outName = val.First();
                   logger.Info(() => "output name overridden as " + outName);
               }),
               new CommandLine.Switch("outpath", val =>
               {
                   outPath = val.First();
                   logger.Info(() => "output path specified as " + outPath);
               }),
               new CommandLine.Switch("cpucount", val =>
               {
                    CPUCount = val.First();
                    if (CPUCount == "all")
                    {
                        CPUCount = Environment.ProcessorCount.ToString();
                        logger.Info(() => "changed to maximum CPUs on system " + CPUCount);
                    }
                    else
                    {
                        if (!int.TryParse(CPUCount, out var CPUCountInt))
                        {
                            logger.Info(() => "unknown cpucount option " + CPUCount);
                        }
                        else
                        {
                            logger.Info(() => "number of CPUs specified as " + CPUCount);
                        }
                    }
               }),
               new CommandLine.Switch("paramsweep", val =>
               {
                   var sweepString = val.ToArray();
                   var sweep = MonteCarloSetup.CreateParameterSweep(sweepString, ParameterSweepType.Count);
                   if (sweep == null) return;
                   paramSweep.Add(sweep);
                   logger.Info(() => "parameter sweep specified as " + sweepString[0] + " from " + sweepString[1] + " to " + sweepString[2] + ", with a count of " + sweepString[3]);
               }),
               new CommandLine.Switch("paramsweepdelta", val =>
               {
                   var sweepString = val.ToArray();
                   var sweep = MonteCarloSetup.CreateParameterSweep(sweepString, ParameterSweepType.Delta);
                   if (sweep == null) return;
                   paramSweep.Add(sweep);
                   logger.Info(() => "parameter sweep specified as " + sweepString[0] + " from " + sweepString[1] + " to " + sweepString[2] + ", with a delta of " + sweepString[3]);
               }),
                new CommandLine.Switch("paramsweeplist", val =>
                {
                    var sweepString = val.ToArray();
                    var sweep = MonteCarloSetup.CreateParameterSweep(sweepString, ParameterSweepType.List);
                    if (sweep == null) return;
                    paramSweep.Add(sweep);
                    logger.Info(() => "parameter sweep specified as " + sweepString[0] + " values");
                }));

            if (!infoOnlyOption)
            {
                Func<SimulationInput, bool> checkValid = simInput =>
                    {
                        var validationResult = MonteCarloSetup.ValidateSimulationInput(simInput);
                        if (validationResult.IsValid) return true;
                        Console.Write("\nSimulation(s) contained one or more errors. Details:");
                        Console.Write("\nValidation rule:" + validationResult.ValidationRule);
                        Console.Write("\nRemarks:" + validationResult.Remarks);
                        Console.Write("\nPress enter key to exit.");
                        Console.Read();
                        return false;
                    };

                if (paramSweep.Any() || inFiles.Any())
                {
                    IList<SimulationInput> inputs;
                    if (paramSweep.Any())
                    {
                        var input = MonteCarloSetup.ReadSimulationInputFromFile(inFile);
                        if (input == null)
                        {
                            return 1;
                        }
                        if (!string.IsNullOrEmpty(outName))
                        {
                            input.OutputName = outName;
                        }

                        inputs = MonteCarloSetup.ApplyParameterSweeps(input, paramSweep).ToList();
                    }
                    else // if infiles.Count() > 0
                    {
                        inputs = inFiles.Select(file => MonteCarloSetup.ReadSimulationInputFromFile(file)).ToList();
                        if (!inputs.Any())
                        {
                            return 1;
                        }
                    }
                    // validate input 
                    if (inputs.Any(simulationInput => !checkValid(simulationInput)))
                    {
                        return 2;
                    }
                    // make sure input does not specify Database if CPUCount>1
                    if (int.Parse(CPUCount) > 1 && (inputs.First().Options.Databases != null && inputs.First().Options.Databases.Count != 0))
                    {
                        CPUCount = 1.ToString();
                        logger.Info(() => "parallel processing cannot be performed when a Database is specified, changed CPUCount to 1");
                    }

                    MonteCarloSetup.RunSimulations(inputs, outPath, int.Parse(CPUCount));
                    logger.Info("\nSimulations complete.");
                    return 0;
                }
                else
                {
                    var input = MonteCarloSetup.ReadSimulationInputFromFile(inFile);
                    if (input == null)
                    {
                        return 1;
                    }

                    if (!checkValid(input))
                        return 2;

                    if (!string.IsNullOrEmpty(outName))
                    {
                        input.OutputName = outName;
                    }
                    // make sure input does not specify Database if CPUCount>1
                    if (int.Parse(CPUCount) > 1 && (input.Options.Databases != null && input.Options.Databases?.Count != 0))
                    {
                        CPUCount = 1.ToString();
                            logger.Info(() => "parallel processing cannot be performed when a Database is specified, changed CPUCount to 1");
                    }
                    MonteCarloSetup.RunSimulation(input, outPath, int.Parse(CPUCount));
                    logger.Info("\nSimulation complete.");
                    return 0;
                }
            }

            LogManager.Configuration = null;
            return 0;
        }
        
        private static void GenerateDefaultInputFiles()
        {
            var inputFiles = SimulationInputProvider.GenerateAllSimulationInputs();
            foreach (var input in inputFiles)
            {
                input.ToFile("infile_" + input.OutputName + ".txt"); // write json to .txt files
            }
            //var sources = SourceInputProvider.GenerateAllSourceInputs();
            //sources.WriteToJson("infile_source_options_test.txt");
        }

        /// <summary>
        /// Displays the help text for detailed usage of the application
        /// </summary>
        private static void ShowHelp()
        {
            logger.Info($"Virtual Photonics MC {GetVersionNumber(3)}");
            logger.Info("\nFor more detailed help type dotnet mc.dll help=<topicname>");
            logger.Info("\ntopics:");
            logger.Info("\ninfile");
            logger.Info("outpath");
            logger.Info("outname");
            logger.Info("cpucount");
            logger.Info("paramsweep");
            logger.Info("paramsweepdelta");
            logger.Info("paramsweeplist");
            logger.Info("\nlist of arguments:");
            logger.Info("\ninfile\t\tthe input file, accepts relative and absolute paths");
            logger.Info("outpath\t\tthe output path, accepts relative and absolute paths");
            logger.Info("outname\t\toutput name, this value is appended for a parameter sweep");
            logger.Info("cpucount\tnumber of CPUs, default is 1");
            logger.Info("paramsweep\ttakes the sweep parameter name and values in the format:");
            logger.Info("\t\tparamsweep=<SweepParameterType>,Start,Stop,Count");
            logger.Info("paramsweepdelta\ttakes the sweep parameter name and values in the format:");
            logger.Info("\t\tparamsweepdelta=<SweepParameterType>,Start,Stop,Delta");
            logger.Info("paramsweeplist\ttakes the sweep parameter name and values in the format:");
            logger.Info("\t\tparamsweeplist=<SweepParameterType>,NumVals,Val1,Val2,...");
            logger.Info("\ngeninfiles\tgenerates example infiles and names them infile_XXX.txt");
            logger.Info("\t\tinfile_XXX.txt where XXX describes the type of input specified");
            logger.Info("\nlist of sweep parameters (SweepParameterType):");
            logger.Info("\nmua1\t\tabsorption coefficient for tissue layer 1");
            logger.Info("mus1\t\tscattering coefficient for tissue layer 1");
            logger.Info("n1\t\trefractive index for tissue layer 1");
            logger.Info("g1\t\tanisotropy for tissue layer 1");
            logger.Info("\nmua2\t\tabsorption coefficient for tissue layer 2");
            logger.Info("mus2\t\tscattering coefficient for tissue layer 2");
            logger.Info("n2\t\trefractive index for tissue layer 2");
            logger.Info("g2\t\tanisotropy for tissue layer 2");
            logger.Info("\nmuai\t\tabsorption coefficient for tissue layer i");
            logger.Info("musi\t\tscattering coefficient for tissue layer i");
            logger.Info("ni\t\trefractive index for tissue layer i");
            logger.Info("gi\t\tanisotropy for tissue layer i");
            logger.Info("\nnphot\t\tnumber of photons to launch from the source");
            logger.Info("\nsample usage:");
            logger.Info("dotnet mc.dll infile=myinput outname=myoutput paramsweep=mua1,0.01,0.04,4 paramsweep=mus1,10,20,2 paramsweep=nphot,1000000,2000000,2\n");
        }

        /// <summary>
        /// Displays the help text for the topic passed as a parameter
        /// </summary>
        /// <param name="helpTopic">Help topic</param>
        private static void ShowHelp(string helpTopic)
        {
            switch (helpTopic.ToLower())
            {
                case "infile":
                    logger.Info("\nINFILE");
                    logger.Info("This is the name of the input file, it can be a relative or absolute path.");
                    logger.Info("If the path name has any spaces enclose it in double quotes.");
                    logger.Info("For relative paths, omit the leading slash.");
                    logger.Info("EXAMPLES for .txt (json) files:");
                    logger.Info("\tinfile=C:\\MonteCarlo\\InputFiles\\myinfile.txt");
                    logger.Info("\tinfile=\"C:\\Monte Carlo\\InputFiles\\myinfile.txt\"");
                    logger.Info("\tinfile=InputFiles\\myinfile.txt");
                    logger.Info("\tinfile=myinfile.txt");
                    break;
                case "outpath":
                    logger.Info("\nOUTPATH");
                    logger.Info("This is the name of the output path, it can be a relative or absolute path.");
                    logger.Info("If the path name has any spaces enclose it in double quotes.");
                    logger.Info("For relative paths, omit the leading slash.");
                    logger.Info("EXAMPLES:");
                    logger.Info("\toutpath=C:\\MonteCarlo\\OutputFiles");
                    logger.Info("\toutpath=OutputFiles");
                    break;
                case "outname":
                    logger.Info("\nOUTNAME");
                    logger.Info("The outname is appended to the folder names if there is a parameter sweep.");
                    logger.Info("EXAMPLE:");
                    logger.Info("\toutname=mcResults");
                    break;
                case "cpucount":
                    logger.Info("\nCPUCOUNT");
                    logger.Info("The cpucount specifies the number of CPUs utilized to process a single simulation.");
                    logger.Info($"The number of CPUs on this computer: {Environment.ProcessorCount}");
                    logger.Info("EXAMPLE:");
                    logger.Info("\tcpucount=4");
                    break;
                case "paramsweep":
                    logger.Info("\nPARAMSWEEP");
                    logger.Info("Defines the parameter sweep and its values.");
                    logger.Info("FORMAT:");
                    logger.Info("\tparamsweep=<SweepParameterType>,Start,Stop,Count");
                    logger.Info("EXAMPLES:");
                    logger.Info("\tparamsweep=mua1,0.01,0.04,4");
                    logger.Info("\tparamsweep=mus1,10,20,2");
                    break;
                case "paramsweepdelta":
                    logger.Info("\nPARAMSWEEPDELTA");
                    logger.Info("Defines the parameter sweep and its values.");
                    logger.Info("FORMAT:");
                    logger.Info("\tparamsweepdelta=<SweepParameterType>,Start,Stop,Delta");
                    logger.Info("EXAMPLES:");
                    logger.Info("\tparamsweepdelta=mua1,0.01,0.04,0.01");
                    logger.Info("\tparamsweepdelta=mus1,10,20,5");
                    break;
                case "paramsweeplist":
                    logger.Info("\nPARAMSWEEPLIST");
                    logger.Info("Defines the parameter sweep and its values.");
                    logger.Info("FORMAT:");
                    logger.Info("\tparamsweeplist=<SweepParameterType>,NumValues,Val1,Val2,Val3,...");
                    logger.Info("EXAMPLES:");
                    logger.Info("\tparamsweeplist=mua1,3,0.01,0.03,0.04");
                    logger.Info("\tparamsweeplist=mus1,5,0.01,1,10,100,1000");
                    break;
                default:
                    ShowHelp();
                    break;
            }
        }

        private static string GetVersionNumber(uint limiter = 0)
        {
            switch (limiter)
            {
                case 1:
                    return
                        $"{Assembly.GetExecutingAssembly().GetName().Version.Major}";
                case 2:
                    return
                        $"{Assembly.GetExecutingAssembly().GetName().Version.Major}.{Assembly.GetExecutingAssembly().GetName().Version.Minor}";
                case 3:
                    return
                        $"{Assembly.GetExecutingAssembly().GetName().Version.Major}.{Assembly.GetExecutingAssembly().GetName().Version.Minor}.{Assembly.GetExecutingAssembly().GetName().Version.Build}";
                default:
                    return
                        $"{Assembly.GetExecutingAssembly().GetName().Version}";

            }
        }
    }
}


