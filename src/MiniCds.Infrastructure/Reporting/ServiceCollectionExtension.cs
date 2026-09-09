// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Reporting\ServiceCollectionExtension.cs
using Microsoft.Extensions.DependencyInjection;
using MiniCds.Application.Reporting;
using MiniCds.Domain.Abstractions;
using MiniCds.Infrastructure.Persistence.Repositories;

namespace MiniCds.Infrastructure.Reporting;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportExporter, CsvReportExporter>();
        services.AddScoped<IReportExporter, PdfReportExporter>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}