/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using DOL.Events;
using log4net;
using System.Reflection;
using DOL.GS.Behaviour.Attributes;

namespace DOL.GS.Behaviour.Triggers
{
    /// <summary>
    /// A trigger defines the circumstances under which a certain QuestAction is fired.
    /// This can be eTriggerAction.Interact, eTriggerAction.GiveItem, eTriggerAction.Attack, etc...
    /// Additional there are two variables to add the needed parameters for the triggertype (Item to give for GiveItem, NPC to interact for Interact, etc...). To fire a QuestAction at least one of the added triggers must be fulfilled.
    /// </summary>
    [Trigger(TriggerType=eTriggerType.PlayerKilled,DefaultValueI=eDefaultValueConstants.NPC)]
    public class PlayerKilledTrigger : AbstractTrigger<Unused,GameNPC>
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Creates a new questtrigger and does some simple triggertype parameter compatibility checking
        /// </summary>
        /// <param name="defaultNPC"></param>
        /// <param name="notifyHandler"></param>
        /// <param name="k"></param>
        /// <param name="i"></param>
        public PlayerKilledTrigger(GameNPC defaultNPC, DOLEventHandler notifyHandler,  object k, object i)
            : base(defaultNPC, notifyHandler, eTriggerType.PlayerKilled, k, i)
        { }

        /// <summary>
        /// Creates a new questtrigger and does some simple triggertype parameter compatibility checking
        /// </summary>
        /// <param name="defaultNPC"></param>
        /// <param name="notifyHandler"></param>
        /// <param name="i"></param>
        public PlayerKilledTrigger(GameNPC defaultNPC, DOLEventHandler notifyHandler, GameNPC i)
            : this(defaultNPC,notifyHandler,  (object)null, (object)i)
        { }

        /// <summary>
        /// Checks the trigger, this method is called whenever a event associated with this questparts quest
        /// or a manualy associated eventhandler is notified.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public override bool Check(DOLEvent e, object sender, EventArgs args)
        {
            bool result = false;

            if (e == GameLivingEvent.EnemyKilled && sender == I)
            {
                GamePlayer player = BehaviourUtils.GuessGamePlayerFromNotify(e, sender, args);
                EnemyKilledEventArgs gArgs = (EnemyKilledEventArgs)args;
                result = gArgs.Target == player;
            }

            return result;
        }

        /// <summary>
        /// Registers the needed EventHandler for this Trigger
        /// </summary>
        /// <remarks>
        /// This method will be called multiple times, so use AddHandlerUnique to make
        /// sure only one handler is actually registered
        /// </remarks>
        public override void Register()
        {
        }

        /// <summary>
        /// Unregisters the needed EventHandler for this Trigger
        /// </summary>
        /// <remarks>
        /// Don't remove handlers that will be used by other triggers etc.
        /// This is rather difficult since we don't know which events other triggers use.
        /// </remarks>
        public override void Unregister()
        {
        }
    }
}
