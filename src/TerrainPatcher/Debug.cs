// Copyright (c) 2021 Esper Thomson
//
// This file is part of TerrainPatcher.
//
// TerrainPatcher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// TerrainPatcher is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with TerrainPatcher.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using QModManager.API;

// TODO Check if we should write some of this stuff to stderr instead of stdout.

namespace TerrainPatcher
{
    // Stuff for debugging.
    internal static class Debug
    {
        // Logs a message.
        [Conditional("DEBUG")]
        public static void Log(string message)
        {
            Console.WriteLine($"[{nameof(TerrainPatcher)}:DEBUG] {message}");
        }

        // Logs an error, with an optional exception.
        public static void LogError(string message, Exception? ex = null)
        {
            Console.WriteLine($"[{nameof(TerrainPatcher)}:ERROR] {message}");

            if (ex is object)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        // Shows an error message on the screen.
        public static void ErrorMessage(string message)
        {
            QModServices.Main.AddCriticalMessage(message);
        }

        // A multi-line message.
        public class Multiline
        {
            public Multiline(string? firstLine = null)
            {
                this.contents = ((firstLine is null) ? "" : " " + firstLine) + Environment.NewLine;
            }

            private string contents;

            public void AddLine(string? line = null)
            {
                this.contents += "\t" + (line ?? "") + Environment.NewLine;
            }

            [Conditional("DEBUG")]
            public void WriteDebug()
            {
                Console.Write($"[{nameof(TerrainPatcher)}:DEBUG] {this.contents}");
            }

            public void WriteError()
            {
                Console.Write($"[{nameof(TerrainPatcher)}:ERROR] {this.contents}");
            }
        }
    }
}
