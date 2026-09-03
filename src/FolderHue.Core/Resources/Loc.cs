using System.Globalization;
using System.Resources;

namespace FolderHue.Core.Resources;

/// <summary>
/// Acces aux chaines localisees du projet.
/// </summary>
/// <remarks>
/// Toutes les chaines affichees a l'utilisateur passent par ici, y compris les titres du menu
/// contextuel : aucun litteral en dur dans l'UI ni dans <c>GetTitle()</c> (CLAUDE.md §7).
/// <para>
/// Cette indirection tres mince a une raison d'etre : si les assemblys satellites posaient
/// probleme a la compilation NativeAOT du shell, seule l'implementation de <see cref="Get"/>
/// changerait, pas ses centaines d'appelants.
/// </para>
/// </remarks>
public static class Loc
{
    private static readonly ResourceManager Manager =
        new("FolderHue.Core.Resources.Strings", typeof(Loc).Assembly);

    /// <summary>Retourne la chaine associee a une cle.</summary>
    /// <param name="key">Nom de la ressource, par exemple <c>Menu_Root</c>.</param>
    /// <returns>
    /// La chaine dans la culture d'interface courante. Si la cle est introuvable, la cle
    /// elle-meme est retournee : un libelle bizarre vaut mieux qu'une exception dans
    /// <c>explorer.exe</c>.
    /// </returns>
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        try
        {
            return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    /// <summary>Retourne une chaine formatee.</summary>
    /// <param name="key">Nom de la ressource, dont la valeur contient des jalons <c>{0}</c>.</param>
    /// <param name="arguments">Les valeurs a injecter.</param>
    /// <returns>La chaine formatee dans la culture d'interface courante.</returns>
    public static string Format(string key, params object?[] arguments)
    {
        string template = Get(key);

        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
