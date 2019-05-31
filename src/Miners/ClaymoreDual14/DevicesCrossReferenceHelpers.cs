﻿using NiceHashMinerLegacy.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClaymoreDual
{
    internal class DevicesCrossReferenceHelpers
    {
        public static async Task<string> ReadLinesUntil(string path, string cwd, string arguments, string fileToTail, IEnumerable<string> breakLines, int timeoutMilliseconds = 30 * 1000)
        {
            var output = new StringBuilder();
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = cwd,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };

                using (var getDevicesHandle = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
                using (var ct = new CancellationTokenSource(timeoutMilliseconds))
                {
                    // start proc
                    getDevicesHandle.Start();
                    Action<string> getDevicesHandleStop = (string stopFrom) => {
                        try
                        {
                            var isRunning = !getDevicesHandle?.HasExited ?? false;
                            if (!isRunning) return;
                            getDevicesHandle.CloseMainWindow();
                            var hasExited = getDevicesHandle?.WaitForExit(1000) ?? false;
                            if (!hasExited) getDevicesHandle.Kill();
                        }
                        catch(Exception e)
                        {
                            // TODO log
                        }
                    };
                    ct.Token.Register(() => getDevicesHandleStop("from cancel token"));
                    // async file tailing
                    using (var semaphore = new SemaphoreSlim(1, 1))
                    using (var fsw = new FileSystemWatcher(cwd))
                    using (var fs = new FileStream(fileToTail, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        fsw.Filter = fileToTail;
                        fsw.EnableRaisingEvents = true;
                        fsw.Changed += (s, e) => semaphore.Release();

                        var line = "";
                        while (!getDevicesHandle.HasExited)
                        {
                            line = await sr.ReadLineAsync();
                            if (line != null)
                            {
                                //Console.WriteLine(line);
                                if (breakLines.Any(breakLine => line.Contains(breakLine)))
                                {
                                    getDevicesHandleStop("from file tail loop");
                                    break;
                                }
                                output.AppendLine(line);
                            }
                            else
                            {
                                await semaphore.WaitAsync(1000);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("ClaymoreDual14.DevicesCrossReferenceHelpers", $"Error occured while getting miner output: {e.Message}");
                return output.ToString();
            }
            return output.ToString();
        }
    }
}
