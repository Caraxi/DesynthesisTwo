using Dalamud.Configuration;
using System;

namespace DesynthesisTwo; 

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool ShowArmoury = false;

    public bool ActualIcons = false;

    public bool HideGearSetItems = true;

    public bool HideNoSkillUp = false;

    public bool ShowDecimals = false;
}