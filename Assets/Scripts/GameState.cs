﻿using SocketIOClient;
using System;
using Unity.VisualScripting;
using UnityEngine;

public enum STATE
{
	INIT,
	PLAYING,
	CONSTRAINT,
	NEW_NPC,
	WRONG
}

public class GameState : MonoBehaviour
{
	public EntityManager _entityManager;
	public MenuManager _menuManager;
	public GameObject WrongMove;

	public STATE _state;

	void Start()
	{
		_state = STATE.INIT;
		gameObject.AddComponent<InitializationSocket>();
		_entityManager = gameObject.AddComponent<EntityManager>();
		_menuManager = gameObject.AddComponent<MenuManager>();
		GameObject.Find("Manager").GetComponent<MessageManager>().gameState = this;
		DontDestroyOnLoad(gameObject);
	}

	public void HandleTangibleEvents(string id, Vector2 pos, float rotation)
	{
		switch(_state)
		{
			case STATE.INIT:
				HandleEventInit(id, pos);
				MovePawnTangible(id, pos, rotation);
				break;
			case STATE.PLAYING:
				MovePawnTangible(id, pos, rotation);
				break;
			case STATE.CONSTRAINT:
				HandleConstraintMove(id, pos, rotation);
				break;
			case STATE.WRONG:
				ReplaceTangible(id, pos, rotation);
				break;
			case STATE.NEW_NPC:
				HandleEventNewNpc(id, pos);
				MovePawnTangible(id, pos, rotation);
				break;
			default:
				break;
		}
		MoveMenuTangible(id, pos, rotation);

	}

	private void HandleEventInit(string id, Vector2 pos)
	{
		if (!_entityManager.Exists(id) && !_menuManager.Exists(id))
		{
			string _idMenu = _menuManager.IsInZone(pos);
			if(_idMenu == "")
			{
				_menuManager.CreateNewMenu(id,pos);
			} else
            {
				_entityManager.CreateNewPlayer(id, pos, _idMenu);
				_menuManager.SetMenuGlobalID(_idMenu, id);
            }
		}
	}

	//NEW_NPC state is used when the GM is creating an NPC. The NPC exists in the EntityManager but needs to be placed.
	//That's what this method does with the first new tangible placed on the table. 
	private void HandleEventNewNpc(string id, Vector2 pos) {
		if (_entityManager.GetEntityWithId(id) == null)
		{
			_entityManager.PlaceNewNpc(id, pos);
		} //TODO maybe add an error message so the GM knows he's (somehow) using a wrong tangible
	}

	public void HandleNotOnTable(string id)
    {
		if (_menuManager.Exists(id) && _state == STATE.INIT && !_menuManager.hasPlayer(id)) 
		{
			_menuManager.HandleNotOnTable(id);
		}
    }

	private void MoveMenuTangible(string id, Vector2 pos, float rotation)
	{
		if (_menuManager.Exists(id))
		{
			_menuManager.Move(id, pos);
			_menuManager.Rotate(id, rotation);
		}
	}
	
	private void MovePawnTangible(string id, Vector2 pos, float rotation)
	{
		if (_entityManager.Exists(id))
		{
			_entityManager.Move(id, pos);
			_entityManager.Rotate(id, rotation);
		}
	}

	// Handle the movement in constraint mode. Modify the state if a move is wrong.
	private void HandleConstraintMove(string id, Vector2 pos, float rotation)
	{
		var player = _entityManager.GetPlayerWithId(id);
		var playerMovement = GameObject.Find("GridManager").GetComponent<PlayerMovement>();
		
		if (_entityManager.Exists(id) && playerMovement.TilePositionChanged(player, pos))
		{
			if (playerMovement.CurMovement == null) // If nobody has asked for moving.
			{
				_state = STATE.WRONG;
				WrongMove.SetActive(true);
			}
			else if (playerMovement.CurMovement != null && playerMovement.CurMovement.Player != player) // If the current movement is not the right player who asked for. 
			{
				_state = STATE.WRONG;
				WrongMove.SetActive(true);
			}
			else if (playerMovement != null) // if the current player is the one who's trying to move in the zone.
			{
				playerMovement.MovePlayer(player, pos, rotation);
			}
		}
	}
	
	
	private void ReplaceTangible(string id, Vector2 pos, float rotation)
	{
		var player = _entityManager.GetPlayerWithId(id);
		var playerMovement = GameObject.Find("GridManager").GetComponent<PlayerMovement>();
		if (player != null)
		{
			playerMovement.ReplacePlayer(player, pos, rotation);
		}
	}
}

