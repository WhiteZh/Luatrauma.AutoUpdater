using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

namespace Luatrauma.AutoUpdater
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Luatrauma.AutoUpdater.Temp");
            Directory.CreateDirectory(tempFolder);

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger.Log("Unhandled exception: " + e.ExceptionObject);
            };

            var rootCommand = new RootCommand("Luatrauma AutoUpdater");

            rootCommand.TreatUnmatchedTokensAsErrors = false;

            var optionServerOnly = new Option<bool>(name: "--server-only", description: "Downloads only the client patch.");
            optionServerOnly.SetDefaultValue(false);
            var optionNightly = new Option<bool>(name: "--nightly", description: "Downloads the nightly patch.");
            optionNightly.SetDefaultValue(false);

            /*
            var argumentRun = new Argument<string[]>("run", "The path to the Barotrauma executable that should be ran after the update finishes.")
            {
                Arity = ArgumentArity.ZeroOrMore
            };
            argumentRun.SetDefaultValue(null);
            */

            //rootCommand.AddArgument(argumentRun);
            rootCommand.AddOption(optionServerOnly);
            rootCommand.AddOption(optionNightly);

            rootCommand.SetHandler(async (InvocationContext ctx) =>
            {
                var nightly = ctx.ParseResult.GetValueForOption(optionNightly);
                var serverOnly = ctx.ParseResult.GetValueForOption(optionServerOnly);

                await Updater.Update(nightly, serverOnly);

                var passthrough = ctx.ParseResult.UnmatchedTokens;

                if (passthrough.Count > 0)
                {
                    string command = string.Join(" ", passthrough);

                    Logger.Log("Starting " + string.Join(" ", command));

                    var info = new ProcessStartInfo
                    {
                        FileName = command,
                        WorkingDirectory = Path.GetDirectoryName(command)
                    };

                    Process.Start(info);
                }
            });

            rootCommand.Invoke(args);
        }
    }
}
