﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Remote.Diagnostics;
using Microsoft.CodeAnalysis.Workspaces.Diagnostics;
using Roslyn.Utilities;
using RoslynLogger = Microsoft.CodeAnalysis.Internal.Log.Logger;

namespace Microsoft.CodeAnalysis.Remote
{
    // root level service for all Roslyn services
    internal partial class CodeAnalysisService : IRemoteDiagnosticAnalyzerService
    {
        /// <summary>
        /// Calculate dignostics. this works differently than other ones such as todo comments or designer attribute scanner
        /// since in proc and out of proc runs quite differently due to concurrency and due to possible amount of data
        /// that needs to pass through between processes
        /// </summary>
        public Task CalculateDiagnosticsAsync(DiagnosticArguments arguments, string pipeName, CancellationToken cancellationToken)
        {
            return RunServiceAsync(async () =>
            {
                // if this analysis is explicitly asked by user, boost priority of this request
                using (RoslynLogger.LogBlock(FunctionId.CodeAnalysisService_CalculateDiagnosticsAsync, arguments.ProjectId.DebugName, cancellationToken))
                using (arguments.ForcedAnalysis ? UserOperationBooster.Boost() : default)
                {
                    // entry point for diagnostic service
                    var solution = await GetSolutionAsync(cancellationToken).ConfigureAwait(false);

                    var projectId = arguments.ProjectId;
                    var analyzers = RoslynServices.AssetService.GetGlobalAssetsOfType<AnalyzerReference>(cancellationToken);

                    var result = await new DiagnosticComputer(solution.GetProject(projectId)).GetDiagnosticsAsync(
                        analyzers, arguments.AnalyzerIds, arguments.ReportSuppressedDiagnostics, arguments.LogAnalyzerExecutionTime, cancellationToken).ConfigureAwait(false);

                    await RemoteEndPoint.WriteDataToNamedPipeAsync(pipeName, result, (writer, data, cancellationToken) =>
                    {
                        var (diagnostics, telemetry, exceptions) = DiagnosticResultSerializer.WriteDiagnosticAnalysisResults(writer, data, cancellationToken);

                        // save log for debugging
                        Log(TraceEventType.Information, $"diagnostics: {diagnostics}, telemetry: {telemetry}, exceptions: {exceptions}");

                        return Task.CompletedTask;
                    }, cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken);
        }

        public void ReportAnalyzerPerformance(List<AnalyzerPerformanceInfo> snapshot, int unitCount, CancellationToken cancellationToken)
        {
            RunService(() =>
            {
                using (RoslynLogger.LogBlock(FunctionId.CodeAnalysisService_ReportAnalyzerPerformance, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var service = SolutionService.PrimaryWorkspace.Services.GetService<IPerformanceTrackerService>();
                    if (service == null)
                    {
                        return;
                    }

                    service.AddSnapshot(snapshot, unitCount);
                }
            }, cancellationToken);
        }

        private static string GetResultLogInfo(DiagnosticAnalysisResultMap<string, DiagnosticAnalysisResultBuilder> result)
        {
            // for now, simple logging
            return $"Analyzer: {result.AnalysisResult.Count}, Telemetry: {result.TelemetryInfo.Count}, Exceptions: {result.Exceptions.Count}";
        }
    }
}
