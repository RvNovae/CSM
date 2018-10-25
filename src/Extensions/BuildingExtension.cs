﻿using CSM.Commands;
using CSM.Helpers;
using CSM.Networking;
using ICities;
using System.Collections.Generic;
using UnityEngine;

namespace CSM.Extensions
{
	public class BuildingExtension : BuildingExtensionBase
	{
		public static Vector3 LastPosition { get; set; }

		/// <summary>
		///     To find a buildings ID we use the position of the building, However when the building is relocated that is no longer possible
		///     this dictionary registre the position of a building when it is created, making it posible to send the old location to the Server/Client.
		/// </summary>

		Dictionary<uint, Vector3> OldPosition = new Dictionary<uint, Vector3>();

		public override void OnCreated(IBuilding building)
		{
			if (!ProtoBuf.Meta.RuntimeTypeModel.Default.IsDefined(typeof(Vector3)))
			{
				ProtoBuf.Meta.RuntimeTypeModel.Default[typeof(Vector3)].SetSurrogate(typeof(Vector3Surrogate));
			}


			/// <summary>
			///    Since the dictionary is lost when the program is terminated, it has to be recreated at startup to ensure that the relocation function doesn't break when loading a saved game
			///    This for-Loop runs through the buildinggrid to extract the location of all buildings on startup and add them to dictionary.  
			///  </summary>

			for (uint i = 0; i < BuildingManager.instance.m_buildingGrid.Length; i++)
			{
				if (BuildingManager.instance.m_buildingGrid[i] != 0)
				{
					var BuildingID = BuildingManager.instance.m_buildingGrid[i];
					OldPosition.Add(BuildingID, BuildingManager.instance.m_buildings.m_buffer[BuildingID].m_position);
				}
			}
		}

		public override void OnBuildingCreated(ushort id)
		{
			base.OnBuildingCreated(id);
			var Instance = BuildingManager.instance;
			var position = Instance.m_buildings.m_buffer[id].m_position;  //the building data is stored in Instance.m_buildings.m_buffer[]
			var angle = Instance.m_buildings.m_buffer[id].m_angle;
			var length = Instance.m_buildings.m_buffer[id].Length;
			var infoindex = Instance.m_buildings.m_buffer[id].m_infoIndex; //by sending the infoindex, the reciever can generate Building_info from the prefap




			if (LastPosition != position)
			{
				switch (MultiplayerManager.Instance.CurrentRole)
				{
					case MultiplayerRole.Server:
						MultiplayerManager.Instance.CurrentServer.SendToClients(CommandBase.BuildingCreatedCommandID, new BuildingCreatedCommand
						{
							BuildingID = id,
							Position = position,
							Infoindex = infoindex,
							Angle = angle,
							Length = length,
						});
						break;

					case MultiplayerRole.Client:
						MultiplayerManager.Instance.CurrentClient.SendToServer(CommandBase.BuildingCreatedCommandID, new BuildingCreatedCommand
						{
							BuildingID = id,
							Position = position,
							Infoindex = infoindex,
							Angle = angle,
							Length = length,
						});
						break;
				}
			}
			OldPosition.Add(id, position); // when a building is created its position is added to the dictionary

			LastPosition = position;
		}

		public override void OnBuildingReleased(ushort id)
		{
			base.OnBuildingReleased(id);
			var position = BuildingManager.instance.m_buildings.m_buffer[id].m_position; //Sending the position of the deleted building is nessesary to calculate the index in M_buildinggrid[index] and get the BuildingID

			switch (MultiplayerManager.Instance.CurrentRole)
			{
				case MultiplayerRole.Server:
					MultiplayerManager.Instance.CurrentServer.SendToClients(CommandBase.BuildingRemovedCommandID, new BuildingRemovedCommand
					{
						Position = position,
					});
					break;

				case MultiplayerRole.Client:
					MultiplayerManager.Instance.CurrentClient.SendToServer(CommandBase.BuildingRemovedCommandID, new BuildingRemovedCommand
					{
						Position = position,
					});
					break;
			}
			OldPosition.Remove(id); // when a building is released its position is removed to the dictionary
		}

		public override void OnBuildingRelocated(ushort id)
		{
			base.OnBuildingRelocated(id);

			/// <summary>
			/// Sends a buildings old position (for identification purpose), its new position and it new angle
			/// </summary>

			var oldPosition = OldPosition[id];
			var newPosition = BuildingManager.instance.m_buildings.m_buffer[id].m_position;
			var angle = BuildingManager.instance.m_buildings.m_buffer[id].m_angle;

			switch (MultiplayerManager.Instance.CurrentRole)
			{
				case MultiplayerRole.Server:
					MultiplayerManager.Instance.CurrentServer.SendToClients(CommandBase.BuildingRelocatedCommandID, new BuildingRelocationCommand
					{
						OldPosition = oldPosition,
						NewPosition = newPosition,
						Angle = angle,

					});
					break;

				case MultiplayerRole.Client:
					MultiplayerManager.Instance.CurrentClient.SendToServer(CommandBase.BuildingRelocatedCommandID, new BuildingRelocationCommand
					{
						OldPosition = oldPosition,
						NewPosition = newPosition,
						Angle = angle,
					});
					break;
			}

			OldPosition.Remove(id);
			OldPosition.Add(id, newPosition);



		}
	}
}