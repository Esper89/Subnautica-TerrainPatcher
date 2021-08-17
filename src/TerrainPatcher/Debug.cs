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

namespace TerrainPatcher
{
    // Stuff for debugging.
    internal static class Debug
    {
        // Logs a message.
        public static void Log(string message)
        {
            Console.WriteLine($"{nameof(TerrainPatcher)}: {message}");
        }

        // Logs an error, with an optional exception.
        public static void LogError(string message, Exception? ex = null)
        {
            // TODO Check if we should write stuff to stderr instead.

            Console.WriteLine($"{nameof(TerrainPatcher)} ERROR: {message}");

            if (ex is object)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        // Shows an error message on the screen.
        public static void ErrorMessage(string message)
        {
            global::ErrorMessage.AddError($"{nameof(TerrainPatcher)}: {message}");
        }
    }
}
