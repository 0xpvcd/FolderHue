using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using FolderHue.Shell.Com;

namespace FolderHue.Shell.Commands;

/// <summary>
/// Enumere les sous-commandes d'un menu en cascade.
/// </summary>
/// <remarks>
/// C'est ce que consomme l'Explorateur pour construire le sous-menu, aussi bien le menu direct de
/// Windows 11 que le menu classique de Windows 10.
/// </remarks>
/// <param name="commands">Les commandes filles, dans l'ordre d'affichage.</param>
[GeneratedComClass]
internal sealed partial class SubCommandEnumerator(IReadOnlyList<object> commands) : IEnumExplorerCommand
{
    private readonly IReadOnlyList<object> _commands = commands;
    private int _position;

    /// <inheritdoc/>
    public int Next(uint celt, IntPtr pUICommand, out uint pceltFetched)
    {
        pceltFetched = 0;

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
                    // On saute la commande fautive plutot que d'interrompre tout le menu.
                    ShellServices.Log.Warn($"Sous-commande {_position} inexposable (HRESULT 0x{hr:X8}).");
                    _position++;
                    continue;
                }

                Marshal.WriteIntPtr(pUICommand, (int)produced * IntPtr.Size, command);
                produced++;
                _position++;
            }

            pceltFetched = produced;

            // S_FALSE signale que l'enumeration a produit moins d'elements que demande.
            return produced == celt ? HResult.Ok : HResult.False;
        }
        catch (Exception e)
        {
            ShellServices.Log.Error("Next a echoue lors de l'enumeration des sous-commandes.", e);
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
            ShellServices.Log.Error("Clone a echoue.", e);
            return HResult.Fail;
        }
    }
}
