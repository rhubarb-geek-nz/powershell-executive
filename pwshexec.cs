// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using Microsoft.PowerShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
#if NET8_0_OR_GREATER
#else
using System.Text;
#endif
using System.Threading;
using System.Threading.Tasks;

namespace RhubarbGeekNz.PowerShellExecutive
{
    internal class Program : IDisposable
    {
        readonly InitialSessionState initialSessionState = InitialSessionState.CreateDefault();
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        PowerShell shell;
        string command = null;
        string inputString = null;
        bool outputString = false;

        private static void SetActionPreference(InitialSessionState initialSessionState, string name, string value)
        {
            initialSessionState.Variables.Add(new SessionStateVariableEntry(name, Enum.Parse(typeof(ActionPreference), value), name));
        }

        private static void SetExecutionPolicy(InitialSessionState initialSessionState, string value)
        {
            initialSessionState.ExecutionPolicy = (ExecutionPolicy)Enum.Parse(typeof(ExecutionPolicy), value);
        }

        private static readonly IDictionary<string, Action<InitialSessionState, string>> actionPreferences = new Dictionary<string, Action<InitialSessionState, string>>()
        {
            { "-debug", (i,s) => SetActionPreference(i, "DebugPreference", s)},
            { "-erroraction", (i,s) => SetActionPreference(i, "ErrorActionPreference", s)},
            { "-informationaction", (i,s) => SetActionPreference(i, "InformationPreference", s)},
            { "-progressaction", (i,s) => SetActionPreference(i, "ProgressPreference", s)},
            { "-verbose",(i,s) => SetActionPreference(i, "VerbosePreference", s)},
            { "-warningaction", (i,s) => SetActionPreference(i, "WarningPreference", s)},
            { "-executionpolicy", (i,s) => SetExecutionPolicy(i, s)}
        };

        internal Program()
        {
            Console.CancelKeyPress += CancelKeyPress;
        }

        public void Dispose()
        {
            Console.CancelKeyPress -= CancelKeyPress;
            cancellationTokenSource.Dispose();
        }

        private void CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            PowerShell powerShell = shell;
            shell = null;
            e.Cancel = true;
            cancellationTokenSource.Cancel();

            if (powerShell != null)
            {
                powerShell.BeginStop(t => powerShell.EndStop(t), powerShell);
            }
        }

        internal static async Task Main(string[] args)
        {
            try
            {
                using (var program = new Program())
                {
                    await program.RunAsync(args);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.ExitCode = 1;
            }
        }

        async Task RunAsync(string[] args)
        {
            int i = 0;

            while (i < args.Length)
            {
                string s = args[i++];

                if (s.StartsWith("-"))
                {
                    if (s.Length == 1)
                    {
                        command = s;

                        break;
                    }

                    string lower = s.ToLower();

                    if (actionPreferences.TryGetValue(lower, out Action<InitialSessionState, string> variable))
                    {
                        variable.Invoke(initialSessionState, args[i++]);
                    }
                    else
                    {
                        switch (lower)
                        {
                            case "-inputstring":
                                inputString = s;
                                break;

                            case "-outputstring":
                                outputString = true;
                                break;

                            default:
                                throw new ArgumentException($"Invalid Argument - {s}");
                        }
                    }
                }
                else
                {
                    command = s;

                    break;
                }
            }

            if (command == null)
            {
                if (inputString != null)
                {
                    throw new ArgumentException($"REPL does not support {inputString}");
                }

                using (Runspace runSpace = RunspaceFactory.CreateRunspace(initialSessionState))
                {
                    runSpace.Open();

                    using (var stream = Console.OpenStandardInput())
                    {
                        using (var reader = new StreamReader(stream, Console.InputEncoding))
                        {
                            CancellationToken cancellationToken = cancellationTokenSource.Token;

                            while (await reader.ReadLineAsync(cancellationToken) is string s)
                            {
#if NETFRAMEWORK
                                using (PowerShell powerShell = PowerShellExtensions.Create(runSpace))
#else
                                using (PowerShell powerShell = PowerShell.Create(runSpace))
#endif
                                {
                                    PSDataCollection<PSObject> input = new PSDataCollection<PSObject>();
                                    PSDataCollection<PSObject> output = new PSDataCollection<PSObject>();

                                    input.Complete();

                                    if (outputString)
                                    {
                                        output.DataAdded += DataAdded;
                                    }

                                    powerShell.AddScript(s);

                                    try
                                    {
                                        shell = powerShell;

                                        powerShell.Invoke(input, output);
                                    }
                                    finally
                                    {
                                        shell = null;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                PSDataCollection<PSObject> input = new PSDataCollection<PSObject>();
                PSDataCollection<PSObject> output = new PSDataCollection<PSObject>();

                if (outputString)
                {
                    output.DataAdded += DataAdded;
                }

                using (PowerShell powerShell = PowerShell.Create(initialSessionState))
                {
                    if ("-".Equals(command))
                    {
                        if (inputString != null)
                        {
                            throw new ArgumentException($"Script from stdin does not support {inputString}");
                        }

                        using (var inputStream = Console.OpenStandardInput())
                        {
                            using (var streamReader = new StreamReader(inputStream, Console.InputEncoding))
                            {
                                powerShell.AddScript(await streamReader.ReadToEndAsync(cancellationTokenSource.Token));
                            }
                        }
                    }
                    else
                    {
                        powerShell.AddCommand(command);
                    }

                    while (i < args.Length)
                    {
                        powerShell.AddArgument(args[i++]);
                    }

                    CancellationToken cancellationToken = cancellationTokenSource.Token;

                    if (inputString == null)
                    {
                        input.Complete();

                        try
                        {
                            shell = powerShell;

                            powerShell.Invoke(input, output);
                        }
                        finally
                        {
                            shell = null;
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException();
                        }
                    }
                    else
                    {
                        try
                        {
                            try
                            {
                                var task = powerShell.InvokeAsync(input, output);

                                try
                                {
                                    if (cancellationToken.IsCancellationRequested)
                                    {
                                        powerShell.Stop();
                                    }
                                    else
                                    {
                                        shell = powerShell;

                                        ReadLineAsync(input, cancellationToken);
                                    }
                                }
                                finally
                                {
                                    await task;
                                }
                            }
                            finally
                            {
                                shell = null;
                            }

                            if (cancellationToken.IsCancellationRequested)
                            {
                                throw new OperationCanceledException();
                            }
                        }
                        finally
                        {
                            cancellationTokenSource.Cancel();
                        }
                    }
                }
            }
        }

        private static void DataAdded(object sender, DataAddedEventArgs e)
        {
            if (sender is PSDataCollection<PSObject> output)
            {
                PSObject element = output[e.Index];

                if (element != null)
                {
                    var obj = element.BaseObject;

                    if (obj is String str)
                    {
                        Console.WriteLine(str);
                    }
                    else
                    {
                        if (obj is ErrorRecord errorRecord)
                        {
                            Console.Error.WriteLine(errorRecord);
                        }
                    }
                }
            }
        }

        async void ReadLineAsync(PSDataCollection<PSObject> input, CancellationToken cancellationToken)
        {
            try
            {
                try
                {
                    using (var stream = Console.OpenStandardInput())
                    {
                        using (var reader = new StreamReader(stream, Console.InputEncoding))
                        {
                            while (await reader.ReadLineAsync(cancellationToken) is string line)
                            {
                                input.Add(new PSObject(line));
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }
            finally
            {
                input.Complete();
            }
        }
    }

#if NET8_0_OR_GREATER
#else
    internal class StreamReader : IDisposable
    {
        readonly Stream input;
        readonly Encoding encoding;
        MemoryStream memory;
        byte[] buffer = new byte[4096];
        int buflen;
        int offset;
        bool lastCR, eof;

        internal StreamReader(Stream input, Encoding encoding)
        {
            this.input = input;
            this.encoding = encoding;
        }

        internal async Task<string> ReadToEndAsync(CancellationToken token)
        {
            MemoryStream stream = new MemoryStream();

            await input.CopyToAsync(stream, buffer.Length, token);

            return encoding.GetString(stream.ToArray());
        }

        internal async Task<string> ReadLineAsync(CancellationToken token)
        {
            string result = null;

            while (true)
            {
                if (offset == buflen)
                {
                    if (eof) break;

                    offset = 0;
                    buflen = await input.ReadAsync(buffer, offset, buffer.Length, token);

                    if (buflen == 0)
                    {
                        eof = true;
                        break;
                    }
                }

                if (lastCR)
                {
                    if (buffer[offset] == '\n')
                    {
                        offset++;
                    }

                    lastCR = false;
                }

                int i = offset;
                bool eol = false;

                while (i < buflen)
                {
                    lastCR = buffer[i] == '\r';

                    if (lastCR || buffer[i] == '\n')
                    {
                        eol = true;
                        break;
                    }

                    i++;
                }

                if (eol)
                {
                    if (memory == null)
                    {
                        result = i == offset ? string.Empty : encoding.GetString(buffer, offset, i - offset);
                    }
                    else
                    {
                        if (i != offset)
                        {
                            memory.Write(buffer, offset, i - offset);
                        }

                        result = encoding.GetString(memory.ToArray());

                        memory = null;
                    }

                    offset = i + 1;

                    if (lastCR && offset < buflen && buffer[offset] == '\n')
                    {
                        lastCR = false;
                        offset++;
                    }

                    break;
                }
                else
                {
                    if (offset < i)
                    {
                        if (memory == null)
                        {
                            memory = new MemoryStream();
                        }

                        memory.Write(buffer, offset, i - offset);

                        offset = i;
                    }
                }
            }

            if (result == null && memory != null)
            {
                result = encoding.GetString(memory.ToArray());

                memory = null;
            }

            return result;
        }

        public void Dispose()
        {
            input.Dispose();
        }
    }
#endif

#if NETFRAMEWORK
    internal static class PowerShellExtensions
    {
        static internal PowerShell Create(Runspace runSpace)
        {
            var powerShell=PowerShell.Create();
            powerShell.Runspace=runSpace;
            return powerShell;
        }

        static internal Task InvokeAsync(this PowerShell powerShell, PSDataCollection<PSObject> input, PSDataCollection<PSObject> output)
        {
             return Task.Factory.FromAsync(powerShell.BeginInvoke(input, output), t => powerShell.EndInvoke(t));
        }
    }
#endif
}
