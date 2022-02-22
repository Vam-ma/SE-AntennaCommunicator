using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        //      Parameters
                                                        // write keyword to timer blocks custom data, don't use whitespaces.

        string external_pb_name_tag = "";               //send messages to this pb as argument. you can have multiple pb with this tag
        string prefix = "";
        string tag = "[ACT]";                           // set to lcd panel names and to timer names
        float text_size = 0.8f;                         // Set text size recommended for cockpits 0.4-0.8, for lcd panels 0.8-1.5
        float text_offset = 10;                         // Set text height offset
        bool basic_display = true;                     // Simple few row display, remove some features and change some arguments.
        bool debug = false;
        Color text = new Color(200, 200, 200);          // RGB 0-255
        Color icons = new Color(10, 200, 200);
        Color background = new Color(0, 0, 0);
        Color selected_item = new Color(200, 0, 0);


        //      Automatic Parameters
        //  *** DONT'T TOUCH ANYTHING BELOW THIS***
        bool first_time_setup, sub_menu, sent_from_argument, change_settings, get_data, use_external_pb, Do_Clear;
        string my_channel, chosen_contact, contact_name, data, source, new_action;
        string _argument;
        int cc_picker, runtime_ticks, cursor, menu;
        IMyBroadcastListener listener;
        IMyTextSurface sf;
        MyCommandLine command_line = new MyCommandLine();
        Dictionary<string, Action> commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Action> remote_commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> contacts = new Dictionary<string, string>();
        Dictionary<string, List<string>> contact_groups = new Dictionary<string, List<string>>();
        List<string> contact_ids = new List<string>();
        List<string> sprite_headers = new List<string>() {"Contacts","Messages","Actions" };
        List<string> debug_list = new List<string>() {"","","", };
        List<string> open_menu = new List<string>();
        List<List<string>> messages = new List<List<string>>();
        List<string> vanilla_Sprites = new List<string>();
        List<string> actions = new List<string>();
        List<string> new_contact = new List<string>();
        List<string> show_data = new List<string>();
        StringBuilder sb = new StringBuilder();
        List<IMyTimerBlock> timer_list = new List<IMyTimerBlock>();
        List<IMyTextPanel> panel_list = new List<IMyTextPanel>();
        List<IMyCockpit> cockpit_list = new List<IMyCockpit>();
        List<IMyProgrammableBlock> external_pb_list = new List<IMyProgrammableBlock>();

        // cd save syntax Contacts_[id]:[name];[another_id]:[another_name]-Actions_[action_name]:[another_action_name]-Groups_[group_name]:[group_member],[another_member];[another_group_name]:[group_member]
        public Program()
        {
            sf = Me.GetSurface(0);
            sf.ContentType = ContentType.TEXT_AND_IMAGE;
            sf.GetSprites(vanilla_Sprites);
            commands["Add_Contact"] = Add_Contact; commands["Add_Action"] = Add_Action; commands["Next"] = Next; commands["Previous"] = Previous; commands["Enter"] = Enter; commands["Cancel"] = Cancel; commands["change"] = Change;
            commands["Send"] = Send; commands["Add_Group"] = add_group; commands["Show"] = Show; commands["Remove"] = Remove; commands["Clear"] = Clear_Data;
            remote_commands["Action"] = Action_;
            Runtime.UpdateFrequency = UpdateFrequency.Once;
        }
        void Main(string argument, UpdateType updateSource)
        {
            sent_from_argument = false;
            if (!first_time_setup || !change_settings) { Setup(); }
            if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) > 0 
            || (updateSource & (UpdateType.Mod)) > 0 
            || (updateSource & (UpdateType.Script)) > 0 )
            {
                if (argument != "")
                {
                    _argument = argument;
                    Get_Blocks();
                    ProsessArgumets(argument);
                }
            }
            if ((updateSource & UpdateType.IGC) > 0)
            {
                Get_Blocks();
                ProcessMessages();
            }
            Handle_Colors();
            Write_Panel();
            runtime_ticks++; if (runtime_ticks > 1) { runtime_ticks = 0; }
        }
        void ProsessArgumets(string argument)
        {
            get_data = false;
            if (command_line.TryParse(argument))
            {
                Action action;
                string command = command_line.Argument(0);
                if (command == null)
                {

                }
                else if(commands.TryGetValue(command,out action))
                {
                    action();
                }
                else
                {
                    if (basic_display)
                    {
                        try { chosen_contact = contact_ids[cc_picker]; contact_name = contacts.GetValueOrDefault(chosen_contact); }
                        catch { try { chosen_contact = contact_ids[0]; contact_name = contacts.GetValueOrDefault(chosen_contact); } catch { return; } }
                        IGC.SendBroadcastMessage(chosen_contact, argument);
                    }
                    else
                    {
                        IGC.SendBroadcastMessage(chosen_contact, argument);
                    }
                }
            }
        }
        void Setup()
        {
            if (Me.CustomName.Contains("AntennaCommunicator Channel:") && Me.CustomName.Contains(Me.GetId().ToString()) && !first_time_setup)
            {
                try
                {
                    string[] get_old_prefix = Me.CustomName.Split(':');
                    string[] old_prefix = get_old_prefix[1].Split('@');
                    if (old_prefix[0].Length <= 5) { prefix = old_prefix[0]; }
                }
                catch { }
            }
            my_channel = Convert.ToString(prefix + "@" + Me.GetId());
            listener = IGC.RegisterBroadcastListener(my_channel);
            listener.SetMessageCallback(my_channel);
            Me.CustomName = "AntennaCommunicator Channel:" + my_channel;
            string[] cd = { }, contacts_ = { }, actions_ = { }, groups_ = { };
            try
            {
                cd = Me.CustomData.Split('-');
                contacts_ = cd[0].Split('_');
                actions_ = cd[1].Split('_');
                groups_ = cd[2].Split('_');
            }
            catch
            {
                Me.CustomData = "Contacts_-Actions_-Groups_";
            }
            string one = "", two = "";
            try
            {
                string[] _contacts = contacts_[1].Split(';');
                foreach (string s in _contacts)
                {
                    string[] id_name = s.Split(':');
                    one = id_name[0];
                    two = id_name[1];
                    if (one.Length > 2 && two.Length > 2)
                    {
                        contacts.Add(one, two);
                        contact_ids.Add(one);
                        List<string> new_message_list = new List<string>();
                        messages.Add(new_message_list);
                        one = ""; two = "";
                    }
                }
            }
            catch { }
            try
            {
                string[] _actions = actions[1].Split(':');
                foreach (string s in _actions)
                {
                    actions.Add(s);
                }
            }
            catch { }
            try
            {
                string[] _groups = groups_[1].Split(';');
                foreach (string s in _groups)
                {
                    string[] group_parts = s.Split(':');
                    string name = group_parts[0];
                    string[] members = group_parts[1].Split(',');
                    List<string> add_group = new List<string>();
                    foreach (string str in members)
                    {
                        add_group.Add(str);
                    }
                    contact_groups.Add(name, add_group);
                }
            }
            catch { }
            Get_Blocks();
            first_time_setup = true; change_settings = true;
        }
        void Clear_Data()
        {
            show_data = new List<string>();
            get_data = true;
            if (command_line.Argument(1) == "yes")
            {
                Do_Clear = true;
                show_data.Add("Run Recompile");
            }
            if (Do_Clear)
            {
                Me.CustomData = "";
            }
            else
            {
                show_data.Add("Do you really wanna Clear all saved data?");
                show_data.Add("if yes, run argument 'Clear yes', else 'cancel'");
            }
            Write_Panel();
        }
        void Remove()
        {
            string[] cd = Me.CustomData.Split('-');
            
            string write_new_data = "";
            List<string> options = new List<string>() { "contact", "group" };
            if (command_line.Argument(1) == options[0])
            {
                try 
                {
                    int id_index = contact_ids.IndexOf(command_line.Argument(2));
                    messages.RemoveAt(id_index);
                    contacts.Remove(command_line.Argument(2));
                    contact_ids.Remove(command_line.Argument(2));
                } catch { }
                try
                {
                    string[] data_groups = cd[0].Split('_');
                    string[] group_parts = data_groups[1].Split(';');
                    foreach(var s in group_parts)
                    {
                        string[] split_values = s.Split(':');
                        if (split_values[0] != command_line.Argument(2))
                        {
                            if (write_new_data.Length < 2)
                            {
                                write_new_data = $"Contacts_{s}";
                            }
                            else
                            {
                                write_new_data = write_new_data + $";{s}";
                            }
                        }
                    }
                }
                catch { }
            }
            if (command_line.Argument(1) == options[1])
            {
                try { contact_groups.Remove(command_line.Argument(2)); } catch { }
                try
                {
                    string[] data_groups = cd[2].Split('_');
                    string[] group_parts = data_groups[1].Split(';');
                    foreach (string s in group_parts)
                    {
                        string[] split_values = s.Split(':');
                        if(split_values[0] != command_line.Argument(2))
                        {
                            if (write_new_data.Length < 2)
                            {
                                write_new_data = $"Groups_{s}";
                            }
                            else
                            {
                                write_new_data = write_new_data + $";{s}";
                            }
                        }
                    }
                    Me.CustomData = $"{cd[0]}-{cd[1]}-{write_new_data}";
                }
                catch { }
            }
        }
        void Change()
        {
            List<string> options = new List<string>() {"prefix", "contact_name", "external_tag", "contact_id" };
            if(command_line.Argument(1) == options[0])
            {
                prefix = command_line.Argument(2);
                contacts = new Dictionary<string, string>();
                contact_ids = new List<string>();
                actions = new List<string>();
                new_contact = new List<string>();
                messages = new List<List<string>>();
                change_settings = false;
                Runtime.UpdateFrequency = UpdateFrequency.Once;
            }
            else if (command_line.Argument(1) == options[1])
            {
                try
                {
                    contacts[command_line.Argument(2)] = command_line.Argument(3);
                }
                catch { }
                try
                {
                    string[] cd = Me.CustomData.Split('-');
                    string[] contacts = cd[0].Split('_');
                    string[] contact_pairs = contacts[1].Split(';');
                    string write_contacts = "", new_value = "";
                    foreach(string s in contact_pairs)
                    {
                        bool equal = false;
                        string[] split_values = s.Split(':');
                        if (split_values[0] == command_line.Argument(2))
                        {
                            new_value = split_values[0] + ":" + command_line.Argument(3);
                            equal = true;
                        }
                        if (write_contacts.Length < 2)
                        {
                            if (equal)
                            {
                                write_contacts = $"Contacts_{new_value}";
                            }
                            else
                            {
                                write_contacts = $"Contacts_{split_values[0]}:{split_values[1]}";
                            }
                        }
                        else
                        {
                            if (equal)
                            {
                                write_contacts = write_contacts + $";{new_value}";
                            }
                            else
                            {
                                write_contacts = write_contacts + $";{split_values[0]}:{split_values[1]}";
                            }
                        }
                    }
                    Me.CustomData = write_contacts + $"-{cd[1]}-{cd[2]}";
                }
                catch { }
            }
            else if (command_line.Argument(1) == options[2])
            {
                if (command_line.Argument(2).Length > 2)
                {
                    external_pb_name_tag = command_line.Argument(2);
                    external_pb_list = new List<IMyProgrammableBlock>();
                    Get_Blocks();
                }
            }
            else if (command_line.Argument(1) == options[3])
            {
                try
                {
                    string name = "";
                    foreach(var contact in contacts)
                    {
                        if(contact.Key == command_line.Argument(2))
                        {
                            name = contact.Value;
                            contacts.Remove(command_line.Argument(2));
                            contacts.Add(command_line.Argument(3), name);
                        }
                    }
                }
                catch { }
                try
                {
                    string[] cd = Me.CustomData.Split('-');
                    string[] contacts = cd[0].Split('_');
                    string[] contact_pairs = contacts[1].Split(';');
                    string write_contacts = "", new_value = "";
                    foreach (string s in contact_pairs)
                    {
                        bool equal = false;
                        string[] split_values = s.Split(':');
                        if (split_values[0] == command_line.Argument(2))
                        {
                            new_value = command_line.Argument(3) + ":" + split_values[1];
                            equal = true;
                        }
                        if (write_contacts.Length < 2)
                        {
                            if (equal)
                            {
                                write_contacts = $"Contacts_{new_value}";
                            }
                            else
                            {
                                write_contacts = $"Contacts_{split_values[0]}:{split_values[1]}";
                            }
                        }
                        else
                        {
                            if (equal)
                            {
                                write_contacts = write_contacts + $";{new_value}";
                            }
                            else
                            {
                                write_contacts = write_contacts + $";{split_values[0]}:{split_values[1]}";
                            }
                        }
                    }
                    Me.CustomData = write_contacts + $"-{cd[1]}-{cd[2]}";
                }
                catch { }
            }
        }
        void Show()
        {
            show_data = new List<string>();
            get_data = true;
            List<string> options = new List<string>() {"contacts","groups","group","actions" };
            if (command_line.Argument(1) == options[0])
            {
                show_data.Add("Contacts:");
                foreach (var contact in contacts)
                {
                    string data = contact.Key + " " + contact.Value;
                    show_data.Add(data);
                }
            }
            else if (command_line.Argument(1) == options[1])
            {
                show_data.Add("Groups:");
                foreach (var group in contact_groups)
                {
                    string data = group.Key;
                    show_data.Add(data);
                }
            }
            else if (command_line.Argument(1) == options[2])
            {
                List<string> try_get_members = new List<string>();
                contact_groups.TryGetValue(command_line.Argument(2), out try_get_members);
                show_data.Add( "Group: " + command_line.Argument(2));
                foreach(var member in try_get_members)
                {
                    string name = "";
                    contacts.TryGetValue(member, out name);
                    show_data.Add(member + " " + name);
                }
            }
            else if(command_line.Argument(1) == options[3])
            {
                show_data.Add("Actions:");
                foreach(string act in actions)
                {
                    show_data.Add(act);
                }
            }
            Write_Panel();
        }
        void Get_Blocks()
        {
            timer_list = new List<IMyTimerBlock>();
            panel_list = new List<IMyTextPanel>();
            cockpit_list = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>(timer_list, x => x.CustomName.Contains(tag));
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(panel_list, x => x.CustomName.Contains(tag));
            GridTerminalSystem.GetBlocksOfType<IMyCockpit>(cockpit_list, x => x.CustomName.Contains(tag));
            if(external_pb_name_tag.Length>1 && external_pb_name_tag != null) { GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(external_pb_list, x => x.CustomName.Contains(external_pb_name_tag)); }
            if (external_pb_list.Count != 0) { use_external_pb = true; }

        }
        void ProcessMessages()
        {
            while (listener.HasPendingMessage)
            {
                Action action;
                var message = listener.AcceptMessage();
                if (message.Tag == my_channel)
                { // This is our tag
                    if (message.Data is string)
                    {
                        data = message.Data.ToString();
                        source = message.Source.ToString();
                        Save_Messages();
                        string[] try_action = data.Split(' ');
                        string command = try_action[0];
                        if (remote_commands.TryGetValue(command, out action))
                        {
                            action();
                        }
                        if (use_external_pb) { foreach(var pb in external_pb_list) { pb.TryRun(data); } }
                    }
                    else // if(msg.Data is XXX)
                    {

                    }
                }
            }
        }
        void Action_()
        {
            Get_Blocks();
            string[] message_ = data.Split(' ');
            foreach(var tim in timer_list)
            {
                if (tim.CustomData.Contains(message_[1]))
                {
                    tim.Trigger();
                }
            }
        }
        void Add_Contact()
        {
            if (!contact_ids.Contains(command_line.Argument(1)))
            {
                try
                {
                    contacts.Add(command_line.Argument(1), command_line.Argument(2));
                    contact_ids.Add(command_line.Argument(1));
                    List<string> new_message_list = new List<string>();
                    messages.Add(new_message_list);
                    new_contact.Add(command_line.Argument(1));
                    new_contact.Add(command_line.Argument(2));
                    Save_Contacts();
                }
                catch
                {
                    contacts.Add(command_line.Argument(1), "No name");
                    contact_ids.Add(command_line.Argument(1));
                    List<string> new_message_list = new List<string>();
                    messages.Add(new_message_list);
                }
            }
        }
        void add_group()
        {
            if (contact_groups.ContainsKey(command_line.Argument(1)))
            {
                List<string> new_group = new List<string>();
                string[] split_s = _argument.Split(' ');
                foreach (string s in split_s)
                {
                    if (s != split_s[0] && s != split_s[1])
                    {
                        new_group.Add(s);
                    }
                }
                contact_groups[command_line.Argument(1)] = new_group;
                Save_old_Group(command_line.Argument(1), new_group);
            }
            else
            {
                List<string> new_group = new List<string>();
                string[] split_s = _argument.Split(' ');
                foreach(string s in split_s)
                {
                    if (s != split_s[0] && s != split_s[1])
                    {
                        new_group.Add(s);
                    }
                }
                contact_groups.Add(command_line.Argument(1), new_group);
                Save_new_Group(command_line.Argument(1), new_group);
            }
        }
        void Add_Action()
        {
            actions.Add(command_line.Argument(1));
            new_action = command_line.Argument(1);
            Save_Actions();
        }
        void Send()
        {
            string[] message = { };
            try
            {
                message = _argument.Split(':');
            }
            catch
            {
                Echo("can't parse message");
            }
            if (contact_groups.ContainsKey(command_line.Argument(1)))
            {
                List<string> group = new List<string>();
                contact_groups.TryGetValue(command_line.Argument(1), out group);
                foreach(string s in group)
                {
                    try
                    {
                        IGC.SendBroadcastMessage(s, message[1]);
                        sent_from_argument = true;
                    }
                    catch
                    {
                        Echo("can't send message");
                    }
                }
            }
            else if (contacts.ContainsValue(command_line.Argument(1)))
            {
                string key = "";
                foreach(var contact in contacts)
                {
                    if(contact.Value == command_line.Argument(1))
                    {
                        key = contact.Key;
                    }
                }
                try
                {
                    IGC.SendBroadcastMessage(key, message[1]);
                    sent_from_argument = true;
                }
                catch
                {
                    Echo("can't send message");
                }
            }
            else
            {
                try
                {
                    IGC.SendBroadcastMessage(command_line.Argument(1), message[1]);
                    sent_from_argument = true;
                }
                catch
                {
                    Echo("can't send message");
                }
            }
        }
        void Next()
        {
            if (basic_display)
            {
                cc_picker++;
                try
                {
                    chosen_contact = contact_ids[cc_picker];
                    contact_name = contacts.GetValueOrDefault(chosen_contact);
                }
                catch
                {
                    cc_picker--;
                }
            }
            else
            {
                switch (menu)
                {
                    case 0:
                        if(cursor< sprite_headers.Count-1)
                        {
                            cursor++;
                        }
                        else { cursor = sprite_headers.Count-1; }
                        break;
                    default:
                        if (cursor < open_menu.Count - 1)
                        {
                            cursor++;
                        }
                        else { cursor = open_menu.Count - 1; }
                        break;
                }
            }
        }
        void Previous()
        {
            if (basic_display)
            {
                cc_picker--;
                try
                {
                    chosen_contact = contact_ids[cc_picker];
                    contact_name = contacts.GetValueOrDefault(chosen_contact);
                }
                catch
                {
                    cc_picker++;
                }
            }
            else
            {
                if (cursor > 0)
                {
                    cursor--;
                }
                else { cursor = 0; }
            }
        }
        void Enter()
        {
            switch (menu)
            {
                case 0:
                    switch (cursor)
                    {
                        case 0:
                            try
                            {
                                open_menu = new List<string>();
                                foreach (string contact in contacts.Values)
                                {
                                    open_menu.Add(contact);
                                }
                                menu = 1;
                            }
                            catch { }
                            break;
                        case 1:
                            try
                            {
                                open_menu = new List<string>();
                                foreach (string message in messages[contact_ids.IndexOf(chosen_contact)])
                                {
                                    open_menu.Add(message);
                                }
                            }
                            catch { open_menu = new List<string>(); open_menu.Add("No Contact \n\r selected"); }
                            menu = 2;
                            break;
                        case 2:
                            try
                            {
                                open_menu = new List<string>();
                                foreach (string action in actions)
                                {
                                    open_menu.Add(action);
                                }
                                menu = 3;
                            }
                            catch { }
                            break;
                    }
                    cursor = 0;
                    break;
                case 1:
                    chosen_contact = contact_ids[cursor];
                    break;
                case 2:
                    break;
                case 3:
                    IGC.SendBroadcastMessage(chosen_contact, "Action " + actions[cursor]);
                    break;
            }
            sub_menu = true;
        }
        void Cancel()
        {
            switch (menu)
            {
                case 0:
                    break;
                default:
                    cursor = 0;
                    sub_menu = false;
                    menu = 0;
                    break;
            }
        }
        void Write_Panel()
        {
            if (basic_display)
            {
                if (!get_data)
                {
                    sb.Clear();
                    sb.Append("My channel: " + my_channel);
                    sb.AppendLine();
                    sb.Append("Contacts count: " + contacts.Count.ToString());
                    sb.AppendLine();
                    if (sent_from_argument)
                    {
                        sb.Append("Send to: " + command_line.Argument(1));
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.Append("Send to: " + chosen_contact);
                        sb.AppendLine();
                    }
                    sb.Append("Name: " + contact_name);
                    sb.AppendLine();
                    sb.Append("Last message:");
                    sb.AppendLine();
                    sb.Append("Source: " + source);
                    sb.AppendLine();
                    sb.Append("Data: " + data);
                    sf.WriteText(sb);
                    foreach (var tb in panel_list)
                    {
                        IMyTextSurface tsf = tb;
                        tsf.ContentType = ContentType.TEXT_AND_IMAGE;
                        tsf.WriteText(sb);
                    }
                    foreach (var cp in cockpit_list)
                    {
                        try
                        {
                            string[] cpcd = cp.CustomData.Split(' ');
                            IMyTextSurface tsf = cp.GetSurface(Convert.ToInt32(cpcd[0]));
                            tsf.ContentType = ContentType.TEXT_AND_IMAGE;
                            tsf.WriteText(sb);
                        }
                        catch { }
                    }
                }
                else
                {
                    sb.Clear();
                    foreach(string s in show_data)
                    {
                        sb.Append(s);
                        sb.AppendLine();
                    }
                    sf.ContentType = ContentType.TEXT_AND_IMAGE;
                    sf.WriteText(sb);
                    foreach (var tb in panel_list)
                    {
                        IMyTextSurface tsf = tb;
                        tsf.ContentType = ContentType.TEXT_AND_IMAGE;
                        tsf.WriteText(sb);
                    }
                    foreach (var cp in cockpit_list)
                    {
                        try
                        {
                            string[] cpcd = cp.CustomData.Split(' ');
                            IMyTextSurface tsf = cp.GetSurface(Convert.ToInt32(cpcd[0]));
                            tsf.ContentType = ContentType.TEXT_AND_IMAGE;
                            tsf.WriteText(sb);
                        }
                        catch { }
                    }
                }
            }
            else
            {
                sf.ContentType = ContentType.SCRIPT;
                sf.Script = "";
                sf.ScriptBackgroundColor = background;
                Write_Panels_Sprite(sf);
                foreach (var tb in panel_list)
                {
                    IMyTextSurface tsf = tb;
                    tsf.ContentType = ContentType.SCRIPT;
                    tsf.Script = "";
                    tsf.ScriptBackgroundColor = background;
                    Write_Panels_Sprite(tsf);
                }
                foreach (var cp in cockpit_list)
                {
                    try
                    {
                        string[] cpcd = cp.CustomData.Split(' ');
                        IMyTextSurface tsf = cp.GetSurface(Convert.ToInt32(cpcd[0]));
                        tsf.ContentType = ContentType.SCRIPT;
                        tsf.Script = "";
                        tsf.ScriptBackgroundColor = background;
                        Write_Panels_Sprite(tsf);
                    }
                    catch { }
                }
            }
            
        }
        void Write_Panels_Sprite(IMyTextSurface surface)
        {
            var frame = surface.DrawFrame();
            Sprites(ref frame, surface);
            frame.Dispose();
        }
        void Handle_Colors()
        {
            if (runtime_ticks == 0) 
            { 
                text.R = text.R++; text.B = text.B++;text.G = text.G++;
                icons.R = icons.R++; icons.B = icons.B++; icons.G = icons.G++;
                selected_item.R = selected_item.R++; selected_item.B = selected_item.B++; selected_item.G = selected_item.G++;
            } 
            else 
            {
                text.R = text.R--; text.B = text.B--; text.G = text.G--;
                icons.R = icons.R--; icons.B = icons.B--; icons.G = icons.G--;
                selected_item.R = selected_item.R--; selected_item.B = selected_item.B--; selected_item.G = selected_item.G--;
            }

        }
        void Sprites(ref MySpriteDrawFrame frame, IMyTextSurface surface)
        {
            var sprite = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = "Communicator",
                Position = new Vector2(surface.SurfaceSize[0] / 2, 30 + text_offset),
                RotationOrScale = text_size + 0.3f,
                FontId = "White",
                Alignment = TextAlignment.CENTER,
                Color = text,
            };
            frame.Add(sprite);
            float next_row_offset = text_size / 0.1f * 1.5f;
            Vector2 pos = new Vector2(30, 50 + text_offset + text_size * 10);
            
            if (sub_menu)
            {
                Vector2 pos_ = new Vector2(surface.SurfaceSize[0] / 2, 60 + text_offset + text_size * 10);
                if(menu == 2)
                {
                    string rows = "";
                    try
                    {
                        string message = open_menu[cursor];
                        int i = 0;
                        foreach (char c in message)
                        {
                            rows = rows + c;
                            if (c == ' ' && i > 8)
                            {
                                rows = rows + "\n\r";
                                i = 0;
                            }
                            i++;
                        }
                    }
                    catch
                    {
                        rows = "No messages";
                    }
                    sprite = new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = rows,
                        Position = pos_,
                        RotationOrScale = text_size + 0.3f,
                        FontId = "White",
                        Alignment = TextAlignment.CENTER,
                        Color = text,
                    };
                    frame.Add(sprite);
                }
                else
                {
                    foreach (var obj in open_menu)
                    {
                        Color sub_menu_color = new Color();
                        if (menu == 1)
                        {
                            if (open_menu.IndexOf(obj) == cursor && contact_ids.IndexOf(chosen_contact) != cursor)
                            {
                                sub_menu_color = selected_item;
                            }
                            else if (open_menu.IndexOf(obj) == cursor && contact_ids.IndexOf(chosen_contact) == cursor)
                            {
                                sub_menu_color = new Color(0, 200, 0);
                            }
                            else { sub_menu_color = text; }
                        }
                        else
                        {
                            if (open_menu.IndexOf(obj) == cursor)
                            {
                                sub_menu_color = selected_item;
                            }
                            else
                            {
                                sub_menu_color = text;
                            }
                        }
                        sprite = new MySprite()
                        {
                            Type = SpriteType.TEXT,
                            Data = obj,
                            Position = pos_,
                            RotationOrScale = text_size + 0.3f,
                            FontId = "White",
                            Alignment = TextAlignment.CENTER,
                            Color = sub_menu_color,
                        };
                        frame.Add(sprite);
                        pos_.Y = pos_.Y + 5 + next_row_offset;
                    }
                }
            }
            else
            {
                foreach (string s in sprite_headers)
                {
                    Color header_color = new Color();
                    if (sprite_headers.IndexOf(s) == cursor)
                    {
                        header_color = selected_item;
                    }
                    else
                    {
                        header_color = text;
                    }
                    sprite = new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = s,
                        Position = pos,
                        RotationOrScale = text_size,
                        FontId = "White",
                        Alignment = TextAlignment.LEFT,
                        Color = header_color,
                    };
                    frame.Add(sprite);
                    pos.Y = pos.Y + 5 + next_row_offset;
                }
            }
            if (debug)
            {
                debug_list[0] = "Cursor: " + cursor.ToString(); debug_list[1] = "Menu: " + menu.ToString();
                Vector2 deb_pos = new Vector2(surface.SurfaceSize[0] / 2, surface.SurfaceSize[1] / 2);
                foreach (string s in debug_list)
                {
                    sprite = new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = s,
                        Position = deb_pos,
                        RotationOrScale = 0.5f,
                        FontId = "White",
                        Alignment = TextAlignment.CENTER,
                        Color = text,
                    };
                    frame.Add(sprite);
                    deb_pos.Y = deb_pos.Y + 20;
                }
            }
        }
        void Save_Messages()
        {
            try
            {
                messages[contact_ids.IndexOf(source)].Add(data);
            }
            catch { }
        }
        void Save_Contacts()
        {
            string[] cd = Me.CustomData.Split('-');
            string[] contacts = cd[0].Split('_');
            string _contacts = contacts[1];
            string add_contact = "";
            string name = "";
            if(new_contact[1]==null || new_contact[1] == "")
            {
                name = "n/a";
            }
            else
            {
                name = new_contact[1];
            }
            if (_contacts.Length < 2)
            {
                add_contact = new_contact[0] + ":" + name;
            }
            else
            {
                add_contact = ";" + new_contact[0] + ":" + name;
            }
            _contacts = _contacts + add_contact;
            string write_contacts = contacts[0] + "_" + _contacts;
            Me.CustomData = write_contacts + "-" + cd[1] + "-" + cd[2];
            new_contact = new List<string>();
        }
        void Save_Actions()
        {
            string[] cd = Me.CustomData.Split('-');
            string[] __actions = cd[1].Split('_');
            string _actions = __actions[1];
            string write_actions = "";
            if (_actions.Length < 2)
            {
                write_actions = __actions[0] + "_" + new_action;
            }
            else
            {
                write_actions = __actions[0] + "_" + new_action + ":" + __actions[1];
            }
            Me.CustomData = cd[0] + "-" + write_actions + "-" + cd[2];
            new_action = "";
        }
        void Save_new_Group(string name, List<string> members)
        {
            string[] cd = Me.CustomData.Split('-');
            string[] groups = cd[2].Split('_');
            string _groups = groups[1];
            string write_groups = "";
            foreach(string s in members)
            {
                if (write_groups.Length < 2)
                {
                    write_groups = $"{name}:{s}";
                }
                else
                {
                    write_groups = write_groups + "," + s;
                }
            }
            if (_groups.Length > 2) { write_groups = write_groups + ";"; }
            Me.CustomData = cd[0] + "-" + cd[1] + "-" + "Groups_" + write_groups + _groups;
        }
        void Save_old_Group(string name, List<string> members)
        {
            string[] cd = Me.CustomData.Split('-');
            string[] groups = cd[2].Split('_');
            string _groups = groups[1];
            string[] group = _groups.Split(';');
            string write_groups = "";
            foreach(var gp in group)
            {
                string[] group_parts = gp.Split(':');
                bool custom = false;
                string new_group = "";
                if(group_parts[0] == name)
                {
                    bool once = false;
                    foreach(var s in members)
                    {
                        if (!once) { once = true; group_parts[1] = ""; }
                        if (group_parts[1].Length < 2)
                        {
                            group_parts[1] = s;
                        }
                        else
                        {
                            group_parts[1] = group_parts[1] + "," + s;
                        }
                    }
                    custom = true;
                    new_group = group_parts[0] + ":" + group_parts[1];
                }
                if (custom)
                {
                    if (write_groups.Length < 2)
                    {
                        write_groups = new_group;
                    }
                    else
                    {
                        write_groups = write_groups + ";" + new_group;
                    }
                }
                else if(write_groups.Length<2)
                {
                    write_groups = gp;
                }
                else
                {
                    write_groups = write_groups + ";" + gp;
                }
            }
            Me.CustomData = cd[0] + "-" + cd[1] + "-" + "Groups_" + write_groups;
        }
    }
}
