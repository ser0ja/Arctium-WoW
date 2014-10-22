﻿/*
 * Copyright (C) 2012-2014 Arctium Emulation <http://arctium.org>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Linq;
using CharacterServer.Attributes;
using CharacterServer.Constants.Character;
using CharacterServer.Constants.Net;
using CharacterServer.Managers;
using CharacterServer.Network;
using CharacterServer.ObjectStores;
using CharacterServer.Packets.Client.Character;
using CharacterServer.Packets.Server.Character;
using CharacterServer.Packets.Structures.Character;
using Framework.Constants.Object;
using Framework.Database;
using Framework.Database.Character.Entities;
using Framework.Network.Packets;
using Framework.Objects;

namespace CharacterServer.Packets.Handlers
{
    class CharacterHandler
    {
        [Message(ClientMessage.EnumCharacters)]
        public static void HandleEnumCharacters(EnumCharacters enumCharacters, CharacterSession session)
        {
            // ORM seems to have problems with session.GameAccount.Id...
            var gameAccount = session.GameAccount;
            var charList = DB.Character.Where<Character>(c => c.GameAccountId == gameAccount.Id);

            var enumCharactersResult = new EnumCharactersResult();

            charList.ForEach(c =>
            {
                enumCharactersResult.Characters.Add(new CharacterListEntry
                {
                    Guid                 = new SmartGuid { Type = GuidType.Player, MapId = (ushort)c.Map, CreationBits = c.Guid },
                    ListPosition         = c.ListPosition,
                    RaceID               = c.Race,
                    ClassID              = c.Class,
                    SexID                = c.Sex,
                    SkinID               = c.Skin,
                    FaceID               = c.Face,
                    HairStyle            = c.HairStyle,
                    HairColor            = c.HairColor,
                    FacialHairStyle      = c.FacialHairStyle,
                    ExperienceLevel      = c.Level,
                    ZoneID               = (int)c.Zone,
                    MapID                = (int)c.Map,
                    PreloadPos           = new Vector3 { X = c.X, Y = c.Y, Z = c.Z },
                    GuildGUID            = new SmartGuid { Type = GuidType.Guild, CreationBits = c.GuildGuid },
                    Flags                = c.CharacterFlags,
                    Flags2               = c.CustomizeFlags,
                    Flags3               = c.Flags3,
                    PetCreatureDisplayID = c.PetCreatureDisplayId,
                    PetExperienceLevel   = c.PetLevel,
                    PetCreatureFamilyID  = c.PetCreatureFamily,
                });
            });

            session.Send(enumCharactersResult);
        }

        [Message(ClientMessage.CreateCharacter)]
        public static void OnCreateCharacter(CreateCharacter createCharacter, CharacterSession session)
        {
            var createChar = new CreateChar { Code = CharCreateCode.InProgress };

            if (!ClientDB.ChrRaces.Any(c => c.Id == createCharacter.RaceID) || !ClientDB.ChrClasses.Any(c => c.Id == createCharacter.ClassID))
                createChar.Code = CharCreateCode.Failed;
            else if (!ClientDB.CharBaseInfo.Any(c => c.RaceId == createCharacter.RaceID && c.ClassId == createCharacter.ClassID))
                createChar.Code = CharCreateCode.Failed;
            else if (DB.Character.Any<Character>(c => c.Name == createCharacter.Name))
                createChar.Code = CharCreateCode.NameInUse;
            else if (createChar.Code == CharCreateCode.InProgress)
            {
                if (createCharacter.TemplateSetID != 0)
                {
                    var accTemplate = session.GameAccount.GameAccountCharacterTemplates.Any(t => t.SetId == createCharacter.TemplateSetID);
                    var realmTemplate = session.Realm.RealmCharacterTemplates.Any(t => t.SetId == createCharacter.TemplateSetID);

                    if (accTemplate || realmTemplate)
                    {
                        var template = DB.Character.Single<CharacterTemplateSet>(s => s.Id == createCharacter.TemplateSetID);

                        // Not implemented = creation failed
                        createChar.Code = CharCreateCode.Failed;
                    }
                    else
                        createChar.Code = CharCreateCode.Failed;
                }
                else
                {
                    var creationData = DB.Character.Single<CharacterCreationData>(d => d.Race == createCharacter.RaceID && d.Class == createCharacter.ClassID);

                    if (creationData != null)
                    {
                        var newChar = new Character
                        {
                            Name            = createCharacter.Name,
                            GameAccountId   = session.GameAccount.Id,
                            RealmId         = session.Realm.Id,
                            Race            = createCharacter.RaceID,
                            Class           = createCharacter.ClassID,
                            Sex             = createCharacter.SexID,
                            Skin            = createCharacter.SkinID,
                            Face            = createCharacter.FaceID,
                            HairStyle       = createCharacter.HairStyleID,
                            HairColor       = createCharacter.HairColorID,
                            FacialHairStyle = createCharacter.FacialHairStyleID,
                            Level           = 1,
                            Map             = creationData.Map,
                            X               = creationData.X,
                            Y               = creationData.Y,
                            Z               = creationData.Z,
                            O               = creationData.O,
                            CharacterFlags  = CharacterFlags.Decline,
                            FirstLogin      = true
                        };

                        if (DB.Character.Add(newChar))
                        {
                            createChar.Code = CharCreateCode.Success;

                            Manager.Character.LearnStartAbilities(newChar);
                        }
                        else
                            createChar.Code = CharCreateCode.Success;
                    }
                    else
                        createChar.Code = CharCreateCode.Failed;
                }
            }

            session.Send(createChar);
        }

        [Message(ClientMessage.CharDelete)]
        public static void OnCharDelete(CharDelete charDelete, CharacterSession session)
        {
            if (charDelete.Guid.CreationBits > 0 && charDelete.Guid.Type == GuidType.Player)
            {
                var deleteChar = new DeleteChar();

                if (DB.Character.Delete<Character>(c => c.Guid == charDelete.Guid.Low && c.GameAccountId == session.GameAccount.Id))
                    deleteChar.Code = CharDeleteCode.Success;
                else
                    deleteChar.Code = CharDeleteCode.Failed;

                session.Send(deleteChar);
            }
            else
                session.Dispose();
        }

        //[Message(ClientMessage.GenerateRandomCharacterName)]
        public static void OnGenerateRandomCharacterName(Packet packet, CharacterSession session)
        {

        }
    }
}
