﻿using CommandLine;
using LibSassHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LibSassBuilder
{
	class Program
	{
        static async Task Main(string[] args)
		{
			var parser = new Parser(config =>
			{
				config.CaseInsensitiveEnumValues = true;
				config.AutoHelp = true;
				config.HelpWriter = Console.Out;
			});

			await parser.ParseArguments<DirectoryOptions, FilesOptions>(args)
				.WithNotParsed(e => Environment.Exit(1))
				.WithParsedAsync(async o =>
				{
					switch (o)
                    {
						case DirectoryOptions directory:
							{
								var program = new Program(directory);

								program.WriteLine($"Sass compile directory: {directory.Directory}");

								await program.CompileDirectoriesAsync(directory.Directory, directory.ExcludedDirectories);

								program.WriteLine("Sass files compiled");
							}
							break;
						case FilesOptions file:
							{
								var program = new Program(file);
								program.WriteLine($"Sass compile files");

								await program.CompileFilesAsync(file.Files);

								program.WriteLine("Sass files compiled");
							}
							break;
						default:
							throw new NotImplementedException("Invalid commandline option parsing");
					}
				});
		}

		public GenericOptions Options { get; }

		public Program(GenericOptions options)
        {
            Options = options;
        }

		async Task CompileDirectoriesAsync(string directory, IEnumerable<string> excludedDirectories)
		{
			var sassFiles = Directory.EnumerateFiles(directory)
				.Where(file => file.EndsWith(".scss", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".sass", StringComparison.OrdinalIgnoreCase));

			await CompileFilesAsync(sassFiles);

			var subDirectories = Directory.EnumerateDirectories(directory);
			foreach (var subDirectory in subDirectories)
			{
				var directoryName = new DirectoryInfo(subDirectory).Name;
				if (excludedDirectories.Any(dir => string.Equals(dir, directoryName, StringComparison.OrdinalIgnoreCase)))
                    			continue;

                		await CompileDirectoriesAsync(subDirectory, excludedDirectories);
			}
		}

		async Task CompileFilesAsync(IEnumerable<string> sassFiles)
		{
            if (Options.OutputLevel >= OutputLevel.Verbose &&
				Options.SassCompilationOptions.IncludePaths is not null &&
                Options.SassCompilationOptions.IncludePaths.Count != 0)
            {
				WriteVerbose($"Include paths:");

				foreach(var path in Options.SassCompilationOptions.IncludePaths)
					WriteVerbose($"\t{path}");
            }

			foreach (var file in sassFiles)
			{
				var fileInfo = new FileInfo(file);
				if (fileInfo.Name.StartsWith("_"))
				{
					WriteVerbose($"Skipping: {fileInfo.FullName}");
					continue;
				}

				WriteVerbose($"Processing: {fileInfo.FullName}");

				var result = SassCompiler.CompileFile(file, options: Options.SassCompilationOptions);

				var newFile = fileInfo.FullName.Replace(fileInfo.Extension, ".css");

				if (File.Exists(newFile) && result.CompiledContent.ReplaceLineEndings() == (await File.ReadAllTextAsync(newFile)).ReplaceLineEndings())
					continue;

				await File.WriteAllTextAsync(newFile, result.CompiledContent);
			}
		}

		void WriteLine(string line)
		{
			if (Options.OutputLevel >= OutputLevel.Default)
			{
				Console.WriteLine(line);
			}
		}

		void WriteVerbose(string line)
		{
			if (Options.OutputLevel >= OutputLevel.Verbose)
			{
				Console.WriteLine(line);
			}
		}
	}
}
