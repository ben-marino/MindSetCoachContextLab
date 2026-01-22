using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindSetCoach.Api.Configuration;
using MindSetCoach.Api.Data;
using MindSetCoach.Api.Services;
using MindSetCoach.Api.Services.AI;
using MindSetCoach.Api.Services.AI.Experiments;
using MindSetCoach.Cli.Commands;

var rootCommand = new RootCommand("MindSetCoach CLI - Run experiments from the command line");

// Add commands
rootCommand.AddCommand(RunCommand.Create());
rootCommand.AddCommand(BatchCommand.Create());
rootCommand.AddCommand(ReportCommand.Create());
rootCommand.AddCommand(ListCommand.Create());
rootCommand.AddCommand(CarouselCommand.Create());

return await rootCommand.InvokeAsync(args);
