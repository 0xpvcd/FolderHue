using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using FolderHue.Shell.Com;

namespace FolderHue.Shell.Commands;

/// <summary>
/// Enumerates the subcommands of a cascading menu.
/// </summary>
/// <remarks>
/// This is what Explorer consumes to build the submenu, in the classic menu of Windows 10 and 11
/// alike.
/// </remarks>
/// <param name="commands">The child commands, in display order.</param>
[GeneratedComClass]
internal sealed partial class SubCommandEnumerator(IReadOnlyList<object> commands) : IEnumExplorerCommand
{
    private readonly IReadOnlyList<object> _commands = commands;
    private int _position;

    /// <inheritdoc/>
    public int Next(uint celt, IntPtr pUICommand, IntPtr pceltFetched)
    {
        // pceltFetched may be NULL when the caller asks for a single element, so the write only
        // happens after a check. This is exactly how Explorer calls us, and writing without
        // checking made Next fail and the submenu vanish (see ComInterop.cs).
        static void Report(IntPtr destination, uint value)
        {
            if (destination != IntPtr.Zero)
            {
                Marshal.WriteInt32(destination, (int)value);
            }
        }

        Report(pceltFetched, 0);

        if (pUICommand == IntPtr.Zero)
        {
            return HResult.Pointer;
        }

        try
        {
            uint produced = 0;

            while (produced < celt && _position < _commands.Count)
            {
                int hr = ShellComWrappers.GetExplorerCommand(_commands[_position], out IntPtr command);

                if (hr < 0 || command == IntPtr.Zero)
                {
                    // Skip the offending command rather than break the whole menu.
                    ShellServices.Log.Warn($"Subcommand {_position} could not be exposed (HRESULT 0x{hr:X8}).");
                    _position++;
                    continue;
                }

                Marshal.WriteIntPtr(pUICommand, (int)produced * IntPtr.Size, command);
                produced++;
                _position++;
            }

            Report(pceltFetched, produced);

            // S_FALSE signals that the enumeration produced fewer elements than requested.
            return produced == celt ? HResult.Ok : HResult.False;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("Next failed while enumerating the subcommands.", e);
            return HResult.Fail;
        }
    }

    /// <inheritdoc/>
    public int Skip(uint celt)
    {
        _position = (int)Math.Min(_commands.Count, _position + (long)celt);
        return HResult.Ok;
    }

    /// <inheritdoc/>
    public int Reset()
    {
        _position = 0;
        return HResult.Ok;
    }

    /// <inheritdoc/>
    public int Clone(out IntPtr ppenum)
    {
        ppenum = IntPtr.Zero;

        try
        {
            var clone = new SubCommandEnumerator(_commands) { _position = _position };
            return ShellComWrappers.GetComInterface(clone, Guids.IEnumExplorerCommand, out ppenum);
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("Clone failed.", e);
            return HResult.Fail;
        }
    }
}
