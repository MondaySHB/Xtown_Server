using System.Collections;
using System.Collections.Generic;
//unity
using UnityEngine;
using UnityEngine.UI;
//photon chat
using Photon.Chat;
using Photon.Realtime;
using AuthenticationValues = Photon.Chat.AuthenticationValues;
//pun
using Photon.Pun;


namespace XReal.Xtown.PhotonChat
{
    public class PhotonChatManager : MonoBehaviour, IChatClientListener {

        //channels
        //list of channels currently subscribed to
        private readonly List<string> myChannels = new List<string>();
        private string selectedChannelName; // mainly used for GUI/input

        // in development
        private readonly List<string> activeUsers = new List<string>();

        //default channels: automatically join on Connect()
        public string[] ChannelsToJoinOnConnect;
        public int HistoryLengthToFetch; 


        
        [Header("Panels")]
        //the ui elements - panels -> set in inspector
        public RectTransform ChatPanel;    
        public Button ShowChatHandle;    

        [Header("UI")]
        //the ui elements - buttons and texts -> set in inspector
        public InputField InputFieldChat; 
        public InputField InputFieldSendTo;
        public Button SendButton;
        public Button HideButton;
        public Text CurrentChannelText;

        //This is the Dropdown
        public Dropdown ChannelDropdown;

        //set in inspector!
        [SerializeField] 
        private string userID;

        ChatClient chatClient;
        // Start is called before the first frame update
        void Start()
        {
            DontDestroyOnLoad(this.gameObject);
            userID = "ChatUser" + UnityEngine.Random.Range(0, 1000).ToString();
            //add listeners to input field and send button
            InputFieldChat.onEndEdit.AddListener(delegate { OnEnterSend(); });
            SendButton.onClick.AddListener(delegate { OnClickSend(); });
            ChannelDropdown.ClearOptions();
            ChannelDropdown.onValueChanged.AddListener(delegate { OnSwitchChannel(); });
            ChatPanel.gameObject.SetActive(false);
            //connect on start
            Connect();
        }


        // Update is called once per frame
        void Update()
        {
            if (this.chatClient != null)
            {
                //check for new messages: call every frame
                this.chatClient.Service();
            }
        }

        /* UI callbacks */

        // on enter pressed
        void OnEnterSend()
        {
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            {
                SendChatMessage(InputFieldChat.text, InputFieldSendTo.text);
                InputFieldChat.text = "";
                InputFieldSendTo.text = "";
            }
        }

        // on send button clicked
        void OnClickSend()
        {
            if (InputFieldChat != null)
            {
                SendChatMessage(InputFieldChat.text, InputFieldSendTo.text);
                InputFieldChat.text = "";
                InputFieldSendTo.text = "";
            }
        }

        // on hide button pressed
        public void OnClickHide()
        {
            if (ChatPanel != null)
            {
                ChatPanel.gameObject.SetActive(false);
                ShowChatHandle.gameObject.SetActive(true);
            }
        }

        // on show button pressed
        public void OnClickShow()
        {
            if (ShowChatHandle != null)
            {
                ChatPanel.gameObject.SetActive(true);
                ShowChatHandle.gameObject.SetActive(false);
            }
        }

        /* Photon Connect */

        void Connect()
        {
            //set up the chat client
            this.chatClient = new ChatClient(this);
            this.chatClient.Connect(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdChat,
                PhotonNetwork.AppVersion, new AuthenticationValues(userID));

            Debug.Log("ChatManager/Connecting as: " + userID);
        }


        /* main parsing function */
        // tooo large for a function. split!!!
        // parse the message(including command)
        void SendChatMessage(string inputLine, string toUser)
        {
            //nothing to send
            if (string.IsNullOrEmpty(inputLine))
            {
                return;
            }

            //is this message private?
            bool doingPrivateChat = !string.IsNullOrEmpty(toUser);
            string privateChatTarget = string.Empty;
            if (doingPrivateChat)
            {
                privateChatTarget = toUser;
            }

            if (inputLine[0].Equals('\\')) //commands begin with character '\'
            {
                string[] tokens = inputLine.Split(new char[] { ' ' }, 2);
                
                //to subscribe to a channel: type "\subscribe <channel name>
                if ((tokens[0].Equals("\\subscribe") || tokens[0].Equals("\\s")) && !string.IsNullOrEmpty(tokens[1]))
                {
                    this.chatClient.Subscribe(tokens[1].Split(new char[] { ' ', ',' }));
                }
                //to unsubscribe a channel: type "\unsubscribe <channel name>
                else if ((tokens[0].Equals("\\unsubscribe") || tokens[0].Equals("\\u")) && !string.IsNullOrEmpty(tokens[1]))
                {
                    this.chatClient.Unsubscribe(tokens[1].Split(new char[] { ' ', ',' }));
                }
                //clearing channel - renove channel / for testing only
                else if (tokens[0].Equals("\\clear"))
                {
                    if (doingPrivateChat)
                    {
                        this.chatClient.PrivateChannels.Remove(this.selectedChannelName);
                    }
                    else //public chat
                    {
                        ChatChannel channel;
                        if (this.chatClient.TryGetChannel(this.selectedChannelName, doingPrivateChat, out channel))
                        {
                            channel.ClearMessages();
                        }
                    }
                }
                //join channel
                else if ((tokens[0].Equals("\\join") || tokens[0].Equals("\\j")) && !string.IsNullOrEmpty(tokens[1]))
                {
                    string[] subtokens = tokens[1].Split(new char[] { ' ', ',' }, 2);

                    //already subscribed to the channel :switch to it
                    if (myChannels.Contains(subtokens[0]))
                    {
                        ShowChannel(subtokens[0]);
                    }
                    else //not yet joined - create a new channel and enter
                    {
                        this.chatClient.Subscribe(new string[] { subtokens[0] });
                    }
                }
                else //invalid input
                {
                    Debug.Log("The command '" + tokens[0] + "' is invalid.");
                }
            }
            else //send messages
            {
                if (doingPrivateChat)
                {
                    Debug.Log("sending private message to " + privateChatTarget);
                    this.chatClient.SendPrivateMessage(privateChatTarget, inputLine);
                }
                else
                {
                    Debug.Log("sending public message" + inputLine);
                    this.chatClient.PublishMessage(this.selectedChannelName, inputLine);
                }
            }
        }

        public void SendHello(string toID)
        {
            this.chatClient.SendPrivateMessage(toID, "user " + userID + " says hi");
        }

        /* Photon Callbacks */
        public void OnConnected()
        {
            //automatic subscription to default channels.
            if (this.ChannelsToJoinOnConnect != null && this.ChannelsToJoinOnConnect.Length > 0)
            {
                this.chatClient.Subscribe(this.ChannelsToJoinOnConnect, this.HistoryLengthToFetch);
            }

            Debug.Log( "Connected as " + this.userID );
            this.ChatPanel.gameObject.SetActive(true);

        }

        public void OnDisconnected()
        {
            Debug.Log("disconnected");
        }

        //switch to channel selected on dropdown
        public void OnSwitchChannel()
        {
            ShowChannel(ChannelDropdown.options[ChannelDropdown.value].text);
        }

        //this updates and shows messages buffered on the channel
        public void ShowChannel(string channelName)
        {

            //get channel by name.
            ChatChannel channel = null;
            bool found = this.chatClient.TryGetChannel(channelName, out channel);
            if (!found)
            {
                Debug.Log("ShowChannel failed to find channel: " + channelName);
                return;
            }

            //switch to this channel
            selectedChannelName = channelName;

            //update selection (dropdown)
            for (int i = 0; i < ChannelDropdown.options.Count; ++i)
            {
                if (ChannelDropdown.options[i].Equals(channelName)) ChannelDropdown.value = i;
            }

            // display private hints
            string[] subtokens = channelName.Split(':');
            if(subtokens[0] == userID)
            {
                InputFieldSendTo.text = subtokens[1];
            }
            else
            {
                InputFieldSendTo.text = "send to..";
            }
            //display message

            CurrentChannelText.text = channel.ToStringMessages();
            Debug.Log("ShowChannel: " + selectedChannelName);

        }

        //get message callbacks
        public void OnGetMessages(string channelName, string[] senders, object[] messages)
        {
            if (channelName.Equals(selectedChannelName))
            {
                ShowChannel(selectedChannelName);
            }
        }

        public void OnPrivateMessage(string sender, object message, string channelName)
        {
            //add to channels.
            if (!myChannels.Contains(channelName))
            {
                myChannels.Add(channelName);

                Dropdown.OptionData option = new Dropdown.OptionData();
                option.text = channelName;
                ChannelDropdown.options.Add(option);


                if (!activeUsers.Exists(id => id == sender))
                {
                    Debug.Log("user " + sender + " added to local list");
                    return;
                }

            }

            
            if (channelName.Equals(this.selectedChannelName))
            {
                ShowChannel(channelName);
            }
        }



        //subscription callbacks
        public void OnSubscribed(string[] channels, bool[] results)
        {
            Dropdown.OptionData option;
            //notify entry on each channel
            foreach (string channel in channels)
            {
                string msg = userID + " subscribed to channel";
                this.chatClient.PublishMessage(channel, msg);

                //add to my channels
                if (!myChannels.Contains(channel))
                {
                    myChannels.Add(channel);
                    option = new Dropdown.OptionData();
                    option.text = channel;
                    ChannelDropdown.options.Add(option);
                }
            }
            Debug.Log("OnSubscribed: " + string.Join(", ", channels));
            
            // Switch to the first newly joined channel
            ShowChannel(channels[0]);
            
        }

        public void OnUnsubscribed(string[] channels)
        {
            foreach (string channelName in channels)
            {
                if (myChannels.Contains(channelName))//if i'm subscribed to the channel
                {
                    Debug.Log("Unsubscribed from channel '" + channelName + "'.");

                    // Showing another channel if the active channel is the one we unsubscribed from
                    if (channelName == selectedChannelName && myChannels.Count > 0)
                    {
                        //get enumerator(like cpp iterator) to the first in the channels.
                        IEnumerator<string> firstEntry = myChannels.GetEnumerator();
                        firstEntry.MoveNext();
                        //IEnumerator.Current gets the element pointed to by the enumerator in the collection
                        ShowChannel(firstEntry.Current);
                    }

                    //remove both from list and dropdown
                    myChannels.Remove(channelName);
                    ChannelDropdown.options.RemoveAll(item => item.text.Equals(channelName));
                }
                else//trying to unsuscribe to a channel not subscribed to.
                {
                    Debug.Log("Can't unsubscribe from channel '" + channelName + "' because you are currently not subscribed to it.");
                }
            }

        }

        public void OnUserSubscribed(string channel, string user)
        {
            Debug.LogFormat("OnUserSubscribed: channel=\"{0}\" userId=\"{1}\"", channel, user);
            activeUsers.Add(user);
            SendHello(user);
        }

        public void OnUserUnsubscribed(string channel, string user)
        {
            Debug.LogFormat("OnUserUnsubscribed: channel=\"{0}\" userId=\"{1}\"", channel, user);
            activeUsers.Remove(user);

        }


        //more code(requires implementation by IChatClientListener interface)

        public void OnStatusUpdate(string user, int status, bool gotMessage, object message)
        {
            throw new System.NotImplementedException();
        }


        public void DebugReturn(ExitGames.Client.Photon.DebugLevel level, string message)
        {
            Debug.Log(message);
        }

        public void OnChatStateChange(ChatState state)
        {
            Debug.Log(state);
        }

        public void OnDestroy()
        {
            if (this.chatClient != null)
            {
                this.chatClient.Disconnect();
            }
        }

        public void OnApplicationQuit()
        {
            if (this.chatClient != null)
            {
                this.chatClient.Disconnect();
            }
        }

    }

}
