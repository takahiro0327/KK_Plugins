﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// KKAPI is not available in the API project so a copy of the required class is included here
// ReSharper disable once CheckNamespace
namespace KKAPI.Utilities
{
    /// <summary>
    /// Gives access to the Windows open file dialog.
    /// http://www.pinvoke.net/default.aspx/comdlg32/GetOpenFileName.html
    /// http://www.pinvoke.net/default.aspx/Structures/OpenFileName.html
    /// http://www.pinvoke.net/default.aspx/Enums/OpenSaveFileDialgueFlags.html
    /// https://social.msdn.microsoft.com/Forums/en-US/2f4dd95e-5c7b-4f48-adfc-44956b350f38/getopenfilename-for-multiple-files?forum=csharpgeneral
    /// </summary>
    internal class OpenFileDialog
    {
        /// <summary>
        /// Arguments used for opening a single file
        /// </summary>
        public const OpenSaveFileDialgueFlags SingleFileFlags = OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST | OpenSaveFileDialgueFlags.OFN_LONGNAMES | OpenSaveFileDialgueFlags.OFN_EXPLORER;

        /// <summary>
        /// Arguments used for opening multiple files
        /// </summary>
        public const OpenSaveFileDialgueFlags MultiFileFlags = SingleFileFlags | OpenSaveFileDialgueFlags.OFN_ALLOWMULTISELECT;

        /// <summary>
        /// Show windows file open dialog. Blocks the thread until user closes the dialog. Returns list of selected files, or null if user cancelled the action.
        /// </summary>
        /// <param name="title">
        /// A string to be placed in the title bar of the dialog box. If this member is NULL, the system uses
        /// the default title (that is, Save As or Open)
        /// </param>
        /// <param name="initialDir">
        /// The initial directory. The algorithm for selecting the initial directory varies on different
        /// platforms.
        /// </param>
        /// <param name="filter">
        /// A list of filter pairs separated by |. First item is the display name, while the second is
        /// the actual filter (e.g. *.txt) Example: <code>"Log files (.log)|*.log|All files|*.*"</code>
        /// </param>
        /// <param name="defaultExt">
        /// The default extension. This extension is appended to the file name if the user fails to type
        /// an extension.
        /// </param>
        /// <param name="flags">
        /// A set of bit flags you can use to initialize the dialog box. When the dialog box returns, it sets these flags to
        /// indicate the user's input.
        /// This member can be a combination of the CommomDialgueFlags.
        /// </param>
        /// <param name="owner">Hwnd pointer of the owner window. IntPtr.Zero to use default parent</param>
        public static string[] ShowDialog(string title, string initialDir, string filter, string defaultExt, OpenSaveFileDialgueFlags flags, IntPtr owner)
        {
            const int MAX_FILE_LENGTH = 2048;

            var ofn = new OpenFileName();

            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = filter.Replace("|", "\0") + "\0";
            ofn.fileTitle = new String(new char[MAX_FILE_LENGTH]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.initialDir = initialDir;
            ofn.title = title;
            ofn.flags = (int)flags;
            if (defaultExt != null)
                ofn.defExt = defaultExt;

            // Create buffer for file names
            var fileNames = new String(new char[MAX_FILE_LENGTH]);
            ofn.file = Marshal.StringToBSTR(fileNames);
            ofn.maxFile = fileNames.Length;

            ofn.dlgOwner = owner;

            // Save and restore working directory after GetOpenFileName changes it
            var currentWorkingDirectory = Environment.CurrentDirectory;
            var success = NativeMethods.GetOpenFileName(ofn);
            Environment.CurrentDirectory = currentWorkingDirectory;
            if (success)
            {
                var selectedFilesList = new List<string>();

                var pointer = (long)ofn.file;
                var file = Marshal.PtrToStringAuto(ofn.file);

                // Retrieve file names
                while (!string.IsNullOrEmpty(file))
                {
                    selectedFilesList.Add(file);

                    pointer += file.Length * 2 + 2;
                    ofn.file = (IntPtr)pointer;
                    file = Marshal.PtrToStringAuto(ofn.file);
                }

                if (selectedFilesList.Count == 1)
                {
                    // Only one file selected with full path
                    return selectedFilesList.ToArray();
                }

                // Multiple files selected, add directory
                var selectedFiles = new string[selectedFilesList.Count - 1];

                for (var i = 0; i < selectedFiles.Length; i++)
                {
                    selectedFiles[i] = selectedFilesList[0] + "\\" + selectedFilesList[i + 1];
                }

                return selectedFiles;
            }
            // "Cancel" pressed
            return null;
        }

        /// <summary>
        /// Show windows file open dialog. Doesn't pause the game.
        /// </summary>
        /// <param name="onAccept">Action that gets called with results of user's selection. Returns list of selected files, or null if user cancelled the action.
        /// WARNING: This runs on another thread! Game will crash if you attempt to access unity methods.
        /// You can use <code>KoikatuAPI.SynchronizedInvoke</code> to go back to the main thread.</param>
        /// <param name="title">
        /// A string to be placed in the title bar of the dialog box. If this member is NULL, the system uses
        /// the default title (that is, Save As or Open)
        /// </param>
        /// <param name="initialDir">
        /// The initial directory. The algorithm for selecting the initial directory varies on different
        /// platforms.
        /// </param>
        /// <param name="filter">
        /// A list of filter pairs separated by |. First item is the display name, while the second is
        /// the actual filter (e.g. *.txt) Example: <code>"Log files (.log)|*.log|All files|*.*"</code>
        /// </param>
        /// <param name="defaultExt">
        /// The default extension. This extension is appended to the file name if the user fails to type
        /// an extension.
        /// </param>
        /// <param name="flags">
        /// A set of bit flags you can use to initialize the dialog box. When the dialog box returns, it sets these flags to
        /// indicate the user's input.
        /// This member can be a combination of the CommomDialgueFlags.
        /// </param>
        public static void Show(Action<string[]> onAccept, string title, string initialDir, string filter, string defaultExt, OpenSaveFileDialgueFlags flags = SingleFileFlags)
        {
            if (onAccept == null) throw new ArgumentNullException(nameof(onAccept));
            var handle = NativeMethods.GetActiveWindow();
            new Thread(
                () =>
                {
                    var result = ShowDialog(title, initialDir, filter, defaultExt, flags, handle);
                    onAccept(result);
                }).Start();
        }

        private static class NativeMethods
        {
            [DllImport("comdlg32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
            public static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

            [DllImport("user32.dll")]
            public static extern IntPtr GetActiveWindow();
        }

        [Flags]
        public enum OpenSaveFileDialgueFlags
        {
            OFN_READONLY = 0x1,
            OFN_OVERWRITEPROMPT = 0x2,
            OFN_HIDEREADONLY = 0x4,
            OFN_NOCHANGEDIR = 0x8,
            OFN_SHOWHELP = 0x10,
            OFN_ENABLEHOOK = 0x20,
            OFN_ENABLETEMPLATE = 0x40,
            OFN_ENABLETEMPLATEHANDLE = 0x80,
            OFN_NOVALIDATE = 0x100,
            OFN_ALLOWMULTISELECT = 0x200,
            OFN_EXTENSIONDIFFERENT = 0x400,
            OFN_PATHMUSTEXIST = 0x800,
            OFN_FILEMUSTEXIST = 0x1000,
            OFN_CREATEPROMPT = 0x2000,
            OFN_SHAREAWARE = 0x4000,
            OFN_NOREADONLYRETURN = 0x8000,
            OFN_NOTESTFILECREATE = 0x10000,
            OFN_NONETWORKBUTTON = 0x20000,

            /// <summary>
            /// Force no long names for 4.x modules
            /// </summary>
            OFN_NOLONGNAMES = 0x40000,

            /// <summary>
            /// New look commdlg
            /// </summary>
            OFN_EXPLORER = 0x80000,
            OFN_NODEREFERENCELINKS = 0x100000,

            /// <summary>
            /// Force long names for 3.x modules
            /// </summary>
            OFN_LONGNAMES = 0x200000,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class OpenFileName
        {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0414 // Field is assigned but its value is never used
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter;
            public string customFilter;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public IntPtr file;
            public int maxFile = 0;
            public string fileTitle;
            public int maxFileTitle = 0;
            public string initialDir;
            public string title;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string defExt;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }
    }
}
