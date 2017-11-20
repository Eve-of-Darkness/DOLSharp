using System;
using DOL.Database;

namespace DOL.GS.Housing
{
    /// <summary>
    /// House item interface.
    /// </summary>
    /// <author>Aredhel</author>
    public interface IHouseHookpointItem
    {
        bool Attach(House house, uint hookpointID, ushort heading);

        bool Attach(House house, DBHouseHookpointItem hookedItem);

        bool Detach(GamePlayer player);

        int Index { get; }

        string TemplateID { get; }
    }
}
