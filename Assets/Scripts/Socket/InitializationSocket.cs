﻿using UnityEngine;
using UnityEngine.UI;
using SocketIOClient;
using System;
using System.Threading;
using Newtonsoft.Json;
using TMPro;
using Newtonsoft.Json;

public class InitializationSocket : MonoBehaviour
{
    public SocketIO _client;
    private Socket socket;
    private GameState _gameState;
    bool firstSwitch = true;
    TurnOrderHandler turnOrderHandler;

    void Start()
    {
        socket = GameObject.Find("SocketClient").GetComponent<Socket>();
        _client = socket.client;
        _gameState = GameObject.Find("TableQuests").GetComponent<GameState>();
        DefineCoroutines();
    }

    public void DefineCoroutines()
    {
        var thread = new Thread(RouteThread);
        thread.Start();

    }

    private void RouteThread()
    {
        while (_client == null)
        {
            _client = socket.client;
            Thread.Sleep(300);
        }

        _client.On("playerConnection", (data) =>
        {
            string str = data.GetValue<string>(0);

            socket._mainThreadhActions.Enqueue(() =>
            {
                _gameState._entityManager.RemoveHelper(str);
            });
        });


        _client.On("switchState", (data) =>
        {
            string str = data.GetValue<string>(0);
            switch (str)
            {
                case "FREE":
                    socket._mainThreadhActions.Enqueue(() =>
                    {
                        if (_gameState._state == STATE.INIT_TURN_ORDER)
                        {
                            resetTurnOrder();
                        }
                        _gameState._state = STATE.PLAYING;
                        Debug.Log("changing to: " + _gameState._state);
                        if (firstSwitch)
                        {
                            _gameState._menuManager.populateMenu();
                            firstSwitch = false;
                        }
                    });
                    break;

                case "RESTRICTED":
                    socket._mainThreadhActions.Enqueue(() =>
                    {
                        if(_gameState._state == STATE.INIT_TURN_ORDER)
                        {
                            resetTurnOrder();
                        }
                        _gameState._state = STATE.CONSTRAINT;
                        Debug.Log("changing to: " + _gameState._state);
                    });
                    break;
                case "INIT_TURN_ORDER":
                    socket._mainThreadhActions.Enqueue(() =>
                    {
                        _gameState._state = STATE.INIT_TURN_ORDER;
                        Debug.Log("changing to: " + _gameState._state);
                    });
                    break;
                case "TURN_ORDER":
                    socket._mainThreadhActions.Enqueue(() =>
                    {
                        _gameState._state = STATE.TURN_ORDER;
                        Debug.Log("changing to: " + _gameState._state);
                    });
                    break;
                default:
                    Debug.Log("State " + str + " is wrong or not implemented yet.");
                    break;
            }
        });


        _client.On("newNpc", (data) =>
        {
            socket._mainThreadhActions.Enqueue(() =>
            {
                string str = data.GetValue<string>(0);
                TempNpc npcData = JsonConvert.DeserializeObject<TempNpc>(str);
                _gameState._entityManager.CreateNewNpc(npcData.id, npcData.name); //normalement cet ID c'est celui du monstre (10: Goblin, 11: Ogre)
            });
        });

        _client.On("updateInfoCharacter", (data) =>
        {
           
            socket._mainThreadhActions.Enqueue(() =>
            {
                List<CharacterUpdateInfo> myObjectList = JsonConvert.DeserializeObject<List<CharacterUpdateInfo>>(data.ToString());
                CharacterUpdateInfo cui = myObjectList[0];
                updateInfoCharacter(cui.playerId, cui.variable, cui.value,false);
            });
        });

        _client.On("updateInfoNpc", (data) =>
        {

            socket._mainThreadhActions.Enqueue(() =>
            {
                List<CharacterUpdateInfo> myObjectList = JsonConvert.DeserializeObject<List<CharacterUpdateInfo>>(data.ToString());
                CharacterUpdateInfo cui = myObjectList[0];
                updateInfoCharacter(cui.playerId, cui.variable, cui.value,true);
            });
        });

        _client.On("turnOrder", (data) =>
        {

            socket._mainThreadhActions.Enqueue(() =>
            {
                List<TurnOrderList> myObjectList = JsonConvert.DeserializeObject<List<TurnOrderList>>(data.ToString());
                TurnOrderList turnOrderList = myObjectList[0];
                createTurnOrder(turnOrderList.list);
            });
        });

        _client.On("characterSelection", (data) =>
        {

            socket._mainThreadhActions.Enqueue(() =>
            {
                List<CharacterSelection> myObjectList = JsonConvert.DeserializeObject<List<CharacterSelection>>(data.ToString());
                CharacterSelection characterSelection = myObjectList[0];
                _gameState._entityManager.GetPlayerWithGlobalId(characterSelection.playerId).name = characterSelection.character;    
            });
        });

        _client.On("turnOrderNext", (data) =>
        {

            socket._mainThreadhActions.Enqueue(() =>
            {
                turnOrderHandler.TurnOrderNext();
            });
        });

        _client.On("pauseGame", (data) =>
        {
            string msg = data.GetValue<string>(0);
            
            socket._mainThreadhActions.Enqueue(() =>
            {
                if(_gameState._state != STATE.PAUSE)
                {   
                    _gameState._previousState = _gameState._state;
                    _gameState._state = STATE.PAUSE;
                    _gameState.WrongMove.SetActive(true);
                    Debug.Log("GameState changed to PAUSE");
                }

                string errorMessage = "";
                string[] everyDisconnectedPlayerIds = msg.Split(",");

                Transform[] previousCanvas = new Transform[_gameState.WrongMove.transform.childCount-1]; //-1 to not remove the text
                for (int i = 1; i < _gameState.WrongMove.transform.childCount; i++)
                {
                    previousCanvas[i-1] = _gameState.WrongMove.transform.GetChild(i);
                }

                foreach (Transform canvas in previousCanvas)
                {
                    Destroy(canvas.gameObject);
                }

                foreach (string playerId in everyDisconnectedPlayerIds)
                {
                    errorMessage += "Player "+playerId+" has disconnected.\n";
                    int pos = Array.IndexOf(everyDisconnectedPlayerIds, playerId);

                    GameObject qrCodeCanvas = Instantiate(Resources.Load("Prefab/QrCodeCanvas") as GameObject, new Vector3((pos+1)*Screen.width/everyDisconnectedPlayerIds.Length, 300, -5), Quaternion.identity);
                    qrCodeCanvas.name = "reconnectionCanvas";
                    GameObject _rawImageReceiver = Instantiate(Resources.Load("Prefab/QrCode") as GameObject, new Vector3((pos+1)*Screen.width/everyDisconnectedPlayerIds.Length, 300, -5), Quaternion.identity);
                    _rawImageReceiver.name = "qrCode"+playerId;
                    GameObject _playerIdText = Instantiate(Resources.Load("Prefab/textID") as GameObject, new Vector3((pos+1)*Screen.width/everyDisconnectedPlayerIds.Length+50, 220, -5), Quaternion.identity);
                    _rawImageReceiver.name = "QrCode" + playerId;
                    _playerIdText.name = playerId;
                    _playerIdText.GetComponent<TextMeshPro>().text = playerId;
                    _playerIdText.transform.SetParent(qrCodeCanvas.transform);
                    _rawImageReceiver.transform.SetParent(qrCodeCanvas.transform);
                    qrCodeCanvas.transform.SetParent(_gameState.WrongMove.transform);

                    string textToEncode = _gameState._entityManager.serverUrl + " " + playerId;
                    Texture2D _storeEncodedTexture = new Texture2D(256, 256);
                    Color32[] _convertPixelToTexture = _gameState._entityManager.EncodeTextToQrCode(textToEncode, _storeEncodedTexture.width, _storeEncodedTexture.height);
                    _storeEncodedTexture.SetPixels32(_convertPixelToTexture);
                    _storeEncodedTexture.Apply();

                    _rawImageReceiver.GetComponent<RawImage>().texture = _storeEncodedTexture;
                }
                _gameState.WrongMove.transform.Find("ErrorMessage").GetComponent<TextMeshPro>().text = errorMessage;
            });
        });

        _client.On("resumeGame", (data) =>
        {
            socket._mainThreadhActions.Enqueue(() =>
            {
                Debug.Log("Received resumeGame");
                _gameState._state = _gameState._previousState;
                _gameState.WrongMove.SetActive(false);
            });
        }); 
        
    }

    public void updateInfoCharacter(string playerId, string variable, string value, bool isNpc)
    {
        Entity character = isNpc ? _gameState._entityManager.GetNPCWithId(playerId) : _gameState._entityManager.GetPlayerWithGlobalId(playerId);
        switch (variable)
        {
            case "life":
                try
                {
                    Debug.Log("CHARACTER LIFE : " + variable);
                    Debug.Log("CHARACTER LIFE : " + value);
                    character.life = int.Parse(value);
                }
                catch (Exception e)
                {
                    Debug.Log("Life value is not numerical: " + e);
                }
                break;
            case "lifeMax":
                try
                {
                    Debug.Log("CHARACTER LIFE : " + variable);
                    Debug.Log("CHARACTER LIFE : " + value);
                    character.lifeMax = int.Parse(value);
                }
                catch (Exception e)
                {
                    Debug.Log("LifeMax value is not numerical: " + e);
                }
                break;
            case "mana":
                try
                {
                    Debug.Log("CHARACTER LIFE : " + variable);
                    Debug.Log("CHARACTER LIFE : " + value);
                    character.mana = int.Parse(value);
                }
                catch (Exception e)
                {
                    Debug.Log("Mana value is not numerical: " + e);
                }
                break;
            case "manaMax":
                try
                {
                    Debug.Log("CHARACTER LIFE : " + variable);
                    Debug.Log("CHARACTER LIFE : " + value);
                    character.manaMax = int.Parse(value);
                }
                catch (Exception e)
                {
                    Debug.Log("ManaMax value is not numerical: " + e);
                }
                break;
        }
    }

    public void createTurnOrder(List<string> listID)
    {
        GameObject canvas = GameObject.Find("CanvasTurnOrder");
        GameObject cardTemplate = Resources.Load("Prefab/EntityCard", typeof(GameObject)) as GameObject;
        Transform panelTransform = canvas.transform.GetChild(0);
        GameObject g;
        canvas.GetComponent<Canvas>().enabled = true;
        turnOrderHandler = new TurnOrderHandler();
        List<Entity> turnOrderListEntity = new List<Entity>();
        foreach (string id in listID)
        {
            Entity entity = _gameState._entityManager.GetEntityWithGlobalId(id);
            if(entity != null)
            {
                turnOrderListEntity.Add(entity);
                g = Instantiate(cardTemplate, panelTransform);
                CardHandler cardHandler = g.AddComponent<CardHandler>();
                cardHandler.turnOrderHandler = turnOrderHandler;
                if (entity.manaMax == 0)
                {
                    cardHandler.Initialize(entity, false);
                } else
                {
                    cardHandler.Initialize(entity, true);
                }
            }
        }
        turnOrderHandler.TurnOrderOn(turnOrderListEntity);
    }

    public void resetTurnOrder()
    {
        GameObject canvas = GameObject.Find("CanvasTurnOrder");
        foreach (Transform child in canvas.transform.GetChild(0).transform)
        {
            Destroy(child.gameObject);
        }
        canvas.GetComponent<Canvas>().enabled = false;
    }
}

    


    [Serializable]
    public class CharacterUpdateInfo
    {
        public CharacterUpdateInfo(string playerId, string variable, string value)
        {
            this.playerId = playerId;
            this.variable = variable;
            this.value = value;
        }

        public string playerId;
        public string variable;
        public string value;
    }

    public class TurnOrderList
    {
        public TurnOrderList(List<string> list)
        {
            this.list = list;
        }

        public List<string> list;
    }

    public class CharacterSelection
    {
        public CharacterSelection(string playerId, string character)
        {
            this.playerId = playerId;
            this.character = character;
        }

        public string playerId;
        public string character;
    }


    public class TempNpc
    {
        public string description;
        public int id;
        public int life;
        public int lifeMax;
        public string name;
    }
}
