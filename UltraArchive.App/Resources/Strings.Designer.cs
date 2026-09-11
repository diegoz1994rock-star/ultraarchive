#nullable enable

using System.Globalization;
using System.Resources;

namespace UltraArchive.App.Resources;

/// <summary>
/// Acceso fuertemente tipado a los recursos localizados de Strings.resx / Strings.en.resx.
/// Escrito a mano (en vez de generado por el diseñador de Visual Studio) para no depender de
/// herramientas de IDE; el SDK de .NET empaqueta igualmente los .resx como recursos incrustados
/// y genera automáticamente el ensamblado satélite "en" a partir del sufijo de cultura del archivo.
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("UltraArchive.App.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string name) =>
        ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string AppTitle => Get(nameof(AppTitle));

    public static string ButtonAdd => Get(nameof(ButtonAdd));
    public static string ButtonExtract => Get(nameof(ButtonExtract));
    public static string ButtonOpen => Get(nameof(ButtonOpen));
    public static string ButtonCompress => Get(nameof(ButtonCompress));
    public static string ButtonPassword => Get(nameof(ButtonPassword));
    public static string ButtonTest => Get(nameof(ButtonTest));
    public static string ButtonIso => Get(nameof(ButtonIso));
    public static string ButtonDelete => Get(nameof(ButtonDelete));

    public static string ColumnName => Get(nameof(ColumnName));
    public static string ColumnType => Get(nameof(ColumnType));
    public static string ColumnSize => Get(nameof(ColumnSize));
    public static string ColumnCompressedSize => Get(nameof(ColumnCompressedSize));
    public static string ColumnDate => Get(nameof(ColumnDate));
    public static string ColumnMethod => Get(nameof(ColumnMethod));
    public static string ColumnProtected => Get(nameof(ColumnProtected));

    public static string StatusReady => Get(nameof(StatusReady));
    public static string StatusNoFileOpen => Get(nameof(StatusNoFileOpen));
    public static string StatusFormatDetectedNoEngine => Get(nameof(StatusFormatDetectedNoEngine));
    public static string StatusArchiveOpened => Get(nameof(StatusArchiveOpened));
    public static string StatusUnknownFormat => Get(nameof(StatusUnknownFormat));
    public static string StatusFeatureComingSoon => Get(nameof(StatusFeatureComingSoon));
    public static string StatusOpenCancelled => Get(nameof(StatusOpenCancelled));

    public static string DialogOpenArchiveTitle => Get(nameof(DialogOpenArchiveTitle));
    public static string DialogOpenArchiveFilter => Get(nameof(DialogOpenArchiveFilter));
    public static string DialogOpenIsoTitle => Get(nameof(DialogOpenIsoTitle));
    public static string DialogOpenIsoFilter => Get(nameof(DialogOpenIsoFilter));
    public static string ButtonIsoTooltip => Get(nameof(ButtonIsoTooltip));
    public static string DialogSelectFolderTitle => Get(nameof(DialogSelectFolderTitle));

    public static string TypeFolder => Get(nameof(TypeFolder));
    public static string TypeFile => Get(nameof(TypeFile));
    public static string ProtectedYes => Get(nameof(ProtectedYes));
    public static string ProtectedNo => Get(nameof(ProtectedNo));

    public static string ThemeToggleToLight => Get(nameof(ThemeToggleToLight));
    public static string ThemeToggleToDark => Get(nameof(ThemeToggleToDark));

    public static string StatusExtracting => Get(nameof(StatusExtracting));
    public static string StatusExtractComplete => Get(nameof(StatusExtractComplete));
    public static string StatusExtractCompleteWithBlocked => Get(nameof(StatusExtractCompleteWithBlocked));
    public static string StatusExtractCancelled => Get(nameof(StatusExtractCancelled));
    public static string StatusExtractSkippedSuffix => Get(nameof(StatusExtractSkippedSuffix));
    public static string StatusTesting => Get(nameof(StatusTesting));
    public static string StatusTestOk => Get(nameof(StatusTestOk));
    public static string StatusTestFailed => Get(nameof(StatusTestFailed));
    public static string StatusCompressing => Get(nameof(StatusCompressing));
    public static string StatusCompressComplete => Get(nameof(StatusCompressComplete));
    public static string StatusCompressCancelled => Get(nameof(StatusCompressCancelled));
    public static string ButtonCancelOperation => Get(nameof(ButtonCancelOperation));

    public static string ShellProgressTitleExtract => Get(nameof(ShellProgressTitleExtract));
    public static string ShellProgressElapsed => Get(nameof(ShellProgressElapsed));
    public static string ShellProgressButtonBackground => Get(nameof(ShellProgressButtonBackground));
    public static string ShellProgressPreparing => Get(nameof(ShellProgressPreparing));

    public static string CompressWindowTitle => Get(nameof(CompressWindowTitle));
    public static string CompressLabelSources => Get(nameof(CompressLabelSources));
    public static string CompressButtonAddFiles => Get(nameof(CompressButtonAddFiles));
    public static string CompressButtonAddFolder => Get(nameof(CompressButtonAddFolder));
    public static string CompressButtonRemoveSelected => Get(nameof(CompressButtonRemoveSelected));
    public static string CompressLabelOutput => Get(nameof(CompressLabelOutput));
    public static string CompressButtonBrowseOutput => Get(nameof(CompressButtonBrowseOutput));
    public static string CompressLabelFormat => Get(nameof(CompressLabelFormat));
    public static string CompressLabelLevel => Get(nameof(CompressLabelLevel));
    public static string CompressCheckboxPreserveStructure => Get(nameof(CompressCheckboxPreserveStructure));
    public static string CompressCheckboxDeleteSource => Get(nameof(CompressCheckboxDeleteSource));
    public static string CompressConfirmDeleteSource => Get(nameof(CompressConfirmDeleteSource));
    public static string CompressButtonStart => Get(nameof(CompressButtonStart));
    public static string CompressStatusReady => Get(nameof(CompressStatusReady));
    public static string CompressNoteFutureFeatures => Get(nameof(CompressNoteFutureFeatures));
    public static string CompressErrorNoSources => Get(nameof(CompressErrorNoSources));
    public static string CompressDropHint => Get(nameof(CompressDropHint));
    public static string CompressDropOverlay => Get(nameof(CompressDropOverlay));
    public static string CompressErrorFormatNotCreatable => Get(nameof(CompressErrorFormatNotCreatable));
    public static string CompressErrorSplitInvalid => Get(nameof(CompressErrorSplitInvalid));
    public static string CompressNoteSplitUnavailable => Get(nameof(CompressNoteSplitUnavailable));
    public static string CompressLabelSplit => Get(nameof(CompressLabelSplit));
    public static string CompressLabelSplitPartSize => Get(nameof(CompressLabelSplitPartSize));
    public static string CompressLabelSplitPartCount => Get(nameof(CompressLabelSplitPartCount));
    public static string CompressStatusVerifying => Get(nameof(CompressStatusVerifying));
    public static string CompressSplitVerifyOk => Get(nameof(CompressSplitVerifyOk));
    public static string CompressSplitVerifyFailed => Get(nameof(CompressSplitVerifyFailed));
    public static string CompressSplitVerifyError => Get(nameof(CompressSplitVerifyError));
    public static string CompressStatusSplitComplete => Get(nameof(CompressStatusSplitComplete));
    public static string CompressErrorNoOutput => Get(nameof(CompressErrorNoOutput));
    public static string CompressCheckboxEncrypt => Get(nameof(CompressCheckboxEncrypt));
    public static string CompressLabelPassword => Get(nameof(CompressLabelPassword));
    public static string CompressLabelConfirmPassword => Get(nameof(CompressLabelConfirmPassword));
    public static string CompressNoteEncryptionUnavailable => Get(nameof(CompressNoteEncryptionUnavailable));
    public static string CompressNoteEncryptionZipNames => Get(nameof(CompressNoteEncryptionZipNames));
    public static string CompressErrorPasswordEmpty => Get(nameof(CompressErrorPasswordEmpty));
    public static string CompressErrorPasswordMismatch => Get(nameof(CompressErrorPasswordMismatch));

    public static string PasswordDialogTitle => Get(nameof(PasswordDialogTitle));
    public static string PasswordDialogPrompt => Get(nameof(PasswordDialogPrompt));
    public static string PasswordDialogPromptRetry => Get(nameof(PasswordDialogPromptRetry));
    public static string PasswordDialogOk => Get(nameof(PasswordDialogOk));
    public static string PasswordDialogCancel => Get(nameof(PasswordDialogCancel));

    public static string CollisionDialogTitle => Get(nameof(CollisionDialogTitle));
    public static string CollisionDialogPrompt => Get(nameof(CollisionDialogPrompt));
    public static string CollisionDialogApplyToAll => Get(nameof(CollisionDialogApplyToAll));
    public static string CollisionDialogOverwrite => Get(nameof(CollisionDialogOverwrite));
    public static string CollisionDialogSkip => Get(nameof(CollisionDialogSkip));
    public static string CollisionDialogRename => Get(nameof(CollisionDialogRename));
    public static string CollisionDialogCancel => Get(nameof(CollisionDialogCancel));

    public static string FolderTreeHeader => Get(nameof(FolderTreeHeader));

    public static string StatusAdding => Get(nameof(StatusAdding));
    public static string StatusAddComplete => Get(nameof(StatusAddComplete));
    public static string StatusAddNotMutable => Get(nameof(StatusAddNotMutable));
    public static string StatusDeleting => Get(nameof(StatusDeleting));
    public static string StatusDeleteComplete => Get(nameof(StatusDeleteComplete));
    public static string StatusDeleteNotMutable => Get(nameof(StatusDeleteNotMutable));
    public static string StatusDeleteNoSelection => Get(nameof(StatusDeleteNoSelection));
    public static string StatusPasswordSet => Get(nameof(StatusPasswordSet));
    public static string StatusPasswordCancelled => Get(nameof(StatusPasswordCancelled));

    public static string ButtonShellIntegration => Get(nameof(ButtonShellIntegration));
    public static string ButtonShellIntegrationTooltip => Get(nameof(ButtonShellIntegrationTooltip));
    public static string ShellIntegrationConfirmInstall => Get(nameof(ShellIntegrationConfirmInstall));
    public static string ShellIntegrationConfirmUninstall => Get(nameof(ShellIntegrationConfirmUninstall));
    public static string ShellIntegrationInstalled => Get(nameof(ShellIntegrationInstalled));
    public static string ShellIntegrationUninstalled => Get(nameof(ShellIntegrationUninstalled));
    public static string ShellIntegrationError => Get(nameof(ShellIntegrationError));

    public static string Format(string template, params object?[] args) =>
        string.Format(CultureInfo.CurrentUICulture, template, args);
}
