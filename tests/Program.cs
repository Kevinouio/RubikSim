using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RubikSim.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            int count=100,seed=0;
            try
            {
                for(int i=0;i<args.Length;i++)
                {
                    if(args[i]=="--count")count=int.Parse(args[++i]);
                    else if(args[i]=="--seed")seed=int.Parse(args[++i]);
                    else throw new ArgumentException("Unknown argument: "+args[i]);
                }
                if(count<0||count>100000)throw new ArgumentOutOfRangeException(nameof(count));
                Console.WriteLine("RubikSim independent C# checks — "+DateTime.UtcNow.ToString("O"));
                Console.WriteLine(RuntimeInformation.OSDescription+" | "+RuntimeInformation.FrameworkDescription+" | logical CPUs "+Environment.ProcessorCount);
                var watch=Stopwatch.StartNew();
                CoreTests.Run();
                SolverTests.Run();
                SessionTests.Run();
                if(count>0)SolverTests.RunRegression(count,seed);
                else Console.WriteLine("NOT RUN seeded regression (--count 0); run --count 100 for acceptance.");
                Console.WriteLine("PASS all requested independent checks; elapsed "+watch.Elapsed.TotalSeconds.ToString("F3")+" s; peak working set "+(Process.GetCurrentProcess().PeakWorkingSet64/1048576.0).ToString("F1")+" MiB.");
                Console.WriteLine("NOT RUN Unity Editor compilation, Unity rendering, Web build and live cube browser checks by this test executable.");
                return 0;
            }
            catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);return 1;}
        }
    }
}
