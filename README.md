# rhubarb-geek-nz/powershell-executive

This is a bare minimumum executable to bootstrap running a PowerShell script.

It differs from the standard console program in that it uses no profile or environment variables to control its behaviour. It is controlled by the arguments it is given and the stdin input.

The execution environment is truely bare-bones with no special processing of output in the pipeline. It is designed to keep out of the way and allow the script to control all aspects of its execution.

It is not intended as a replacement for the standard `pwsh` or `powershell` console applications.

## Usage

```
$ pwshexec [options...] [-|command] [arguments...]
```

The program operates in one of three modes

Mode | Selection | Comment
-----|-----------|--------
Named command | Script or cmdlet | The command is executed with the given argument list. If `-InputString` was specified then stdin is read and the lines are provided as input pipeline values.
Stdin script | Hyphen as command | The PowerShell script is read in its entirety from stdin and used as a script. Arguments will be passed to the script.
REPL | No command present | Stdin is read line by line and executed as a script.

Where options are from

Option | Values | Comment
-------|--------|--------
-Debug | [ActionPreference](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.actionpreference) | Used to set $DebugPreference
-ErrorAction | [ActionPreference](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.actionpreference) | Used to set $ErrorActionPreference
-InformationAction | [ActionPreference](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.actionpreference) | Used to set $InformationPreference
-ProgressAction | [ActionPreference](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.actionpreference) | Used to set $ProgressPreference
-Verbose | [ActionPreference](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.actionpreference) | Used to set $VerbosePreference
-WarningAction | [ActionPreference](https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.actionpreference) | Used to set $WarningPreference
-ExecutionPolicy | [ExecutionPolicy](https://learn.microsoft.com/en-us/dotnet/api/microsoft.powershell.executionpolicy?view) | Sets the initial session state's execution policy.
-InputString | | Read input strings from stdin and pass as pipeline input to the named command
-OutputString | | Write output pipeline string values to stdout and error records to stderr.

The options are processed in all three operating modes.

The command `-` refers to reading the PowerShell script from stdin.

## PowerShell Versions

Framework | PowerShell SDK | Comment
----------|----------------|--------
net481 | 5.1.1 | .NET Framework. PowerShell is equivalent to WindowsPowerShell.
net6.0 | 7.2.21 | .NET Core 6.0
net8.0 | 7.4.3 | .NET Core 8.0

## Execution environment

Asynchronous programming methods are used in order for pipelines to operate. Ctrl-C is supported to stop the executing PowerShell.

## Example

Without the -OutputString option the command itself will not write to stdout.

```
$ echo '$PSVersionTable | Out-String' | bin/Release/net6.0/pwshexec -OutputString

Name                           Value
----                           -----
PSVersion                      7.2.21
PSEdition                      Core
GitCommitId                    7.2.21
OS                             Linux 6.1.21-v8+ #1642 SMP PREEMPT Mon Apr  3 17:24:16 BST 2023
Platform                       Unix
PSCompatibleVersions           {1.0, 2.0, 3.0, 4.0}
PSRemotingProtocolVersion      2.3
SerializationVersion           1.1.0.1
WSManStackVersion              3.0
```
