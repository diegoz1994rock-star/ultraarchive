#nullable enable
using System;

namespace UltraArchive.ShellExtension;

[Flags]
internal enum EXPCMDSTATE : uint
{
    ECS_ENABLED = 0,
    ECS_DISABLED = 1,
    ECS_HIDDEN = 2,
    ECS_CHECKBOX = 4,
    ECS_CHECKED = 8,
}

[Flags]
internal enum EXPCMDFLAGS : uint
{
    ECF_DEFAULT = 0x000,
    ECF_HASSUBCOMMANDS = 0x001,
    ECF_HASSPLITBUTTON = 0x002,
    ECF_HIDELABEL = 0x004,
    ECF_ISSEPARATOR = 0x008,
    ECF_HASLUASHIELD = 0x010,
    ECF_SEPARATORBEFORE = 0x020,
    ECF_SEPARATORAFTER = 0x040,
    ECF_ISDROPDOWN = 0x080,
    ECF_TOGGLEABLE = 0x100,
    ECF_AUTOMENUICONS = 0x200,
}

internal enum SIGDN : uint
{
    SIGDN_NORMALDISPLAY = 0x00000000,
    SIGDN_FILESYSPATH = 0x80058000,
}
