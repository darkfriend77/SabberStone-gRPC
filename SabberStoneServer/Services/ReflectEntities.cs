using SabberStoneContract.Core;
using SabberStoneCore.Model.Entities;
using BoardZone = SabberStoneContract.Core.BoardZone;
using Controller = SabberStoneContract.Core.Controller;
using DeckZone = SabberStoneContract.Core.DeckZone;
using HandZone = SabberStoneContract.Core.HandZone;
using Hero = SabberStoneContract.Core.Hero;
using HeroPower = SabberStoneContract.Core.HeroPower;
using Minion = SabberStoneContract.Core.Minion;
using SecretZone = SabberStoneContract.Core.SecretZone;
using Weapon = SabberStoneContract.Core.Weapon;

namespace SabberStoneServer.Services
{
    public partial class MatchGameService
    {
        private MatchGame CreateMatchGameReply()
        {
            MatchGame result = new MatchGame()
            {
                GameId = GameId,
                CurrentPlayer = GetController(_game.CurrentPlayer),
                CurrentOpponent = GetController(_game.CurrentOpponent),
                State = (MatchGame.Types.State)_game.State,
                Turn = _game.Turn
        };

            return result;
        }

        private Controller GetController(SabberStoneCore.Model.Entities.Controller controller)
        {
            return new Controller()
            {
                Id = controller.PlayerId,
                Hero = GetHero(controller.Hero),
                BoardZone = GetBoardZone(controller.BoardZone),
                HandZone = GetHandZone(controller.HandZone),
                SecretZone = GetSecretZone(controller.SecretZone),
                DeckZone = GetDeckZone(controller.DeckZone),
                PlayState = (Controller.Types.PlayState)controller.PlayState,
                BaseMana = controller.BaseMana,
                RemainingMana = controller.RemainingMana,
                OverloadLocked = controller.OverloadLocked,
                OverloadOwed = controller.OverloadOwed
            };
        }

        private BoardZone GetBoardZone(SabberStoneCore.Model.Zones.BoardZone zone)
        {
            var result = new BoardZone();
            var span = zone.GetSpan();
            for (int i = 0; i < span.Length; i++)
                result.Minions.Add(GetMinion(span[i]));
            return result;
        }

        private HandZone GetHandZone(SabberStoneCore.Model.Zones.HandZone zone)
        {
            var result = new HandZone();
            var span = zone.GetSpan();
            for (int i = 0; i < span.Length; i++)
                result.Entities.Add(GetPlayable(span[i], true));
            return result;
        }

        private SecretZone GetSecretZone(SabberStoneCore.Model.Zones.SecretZone zone)
        {
            var result = new SecretZone();
            var span = zone.GetSpan();
            for (int i = 0; i < span.Length; i++)
                result.Entities.Add(GetPlayable(span[i], true));
            return result;
        }

        private DeckZone GetDeckZone(SabberStoneCore.Model.Zones.DeckZone zone)
        {
            var result = new DeckZone();
            var span = zone.GetSpan();
            for (int i = 0; i < span.Length; i++)
                result.Entities.Add(GetPlayable(span[i], true));
            return result;
        }

        private PlayableEntity GetPlayable(IPlayable playable, bool hand)
        {
            bool isCharacter = playable is Character;
            Character c = isCharacter ? playable as Character : null;
            return new PlayableEntity()
            {
                CardId = playable.Card.AssetId,
                Cost = playable.Cost,
                Atk = isCharacter ? c.AttackDamage : 0,
                BaseHealth = isCharacter ? c.BaseHealth : 0,
                Ghostly = hand ? playable[SabberStoneCore.Enums.GameTag.GHOSTLY] == 1 : false
            };
        }

        private Minion GetMinion(SabberStoneCore.Model.Entities.Minion minion)
        {
            return new Minion()
            {
                CardId = minion.Card.AssetId,
                Atk = minion.AttackDamage,
                BaseHealth = minion.BaseHealth,
                Damage = minion.Damage,
                NumAttacksThisTurn = minion.NumAttacksThisTurn,
                Exhausted = minion.IsExhausted,
                AttackableByRush = minion.AttackableByRush,
                Charge = minion.HasCharge,
                Windfury = minion.HasWindfury,
                Lifesteal = minion.HasLifeSteal,
                Poisonous = minion.Poisonous,
                Stealth = minion.HasStealth,
                DivineShield = minion.HasDivineShield,
                Immune = minion.IsImmune,
                Elusive = minion.CantBeTargetedBySpells,
                Frozen = minion.IsFrozen,
                Deathrattle = minion.HasDeathrattle,

                ZonePosition = minion.ZonePosition,
                OrderOfPlay = minion.OrderOfPlay
            };
        }

        private Hero GetHero(SabberStoneCore.Model.Entities.Hero hero)
        {
            return new Hero()
            {
                CardClass = (int)hero.Card.Class,
                Atk = hero.AttackDamage,
                BaseHealth = hero.BaseHealth,
                Damage = hero.Damage,
                NumAttacksThisTurn = hero.NumAttacksThisTurn,
                Armor = hero.Armor,
                Exhausted = hero.IsExhausted,
                Stealth = hero.HasStealth,
                Immune = hero.IsImmune,

                Power = GetHeroPower(hero.HeroPower),
                Weapon = hero.Weapon != null ? GetWeapon(hero.Weapon) : null
            };
        }

        private HeroPower GetHeroPower(SabberStoneCore.Model.Entities.HeroPower heroPower)
        {
            return new HeroPower()
            {
                CardId = heroPower.Card.AssetId,
                Cost = heroPower.Cost,
                Exhausted = heroPower.IsExhausted
            };
        }

        private Weapon GetWeapon(SabberStoneCore.Model.Entities.Weapon weapon)
        {
            return new Weapon()
            {
                CardId = weapon.Card.AssetId,
                Atk = weapon.AttackDamage,
                Durability = weapon.Durability,
                Windfury = weapon.IsWindfury,
                Lifesteal = weapon.HasLifeSteal,
                Poisonous = weapon.Poisonous,
                Immune = weapon.IsImmune,
            };
        }

    }
}
