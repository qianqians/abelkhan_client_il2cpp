﻿using System;
using System.Collections;
using System.Threading;

namespace client
{
	public class client
	{
        public delegate void onDisConnectHandle();
        public event onDisConnectHandle onDisConnect;

        public client()
		{
			uuid = System.Guid.NewGuid().ToString();
			timer = new service.timerservice();
			modulemanager = new common.modulemanager();

            log.log.logMode = log.log.enLogMode.Release;

			var _process = new juggle.process();
			_gate_call_client = new module.gate_call_client();
			_gate_call_client.onconnect_gate_sucess += on_ack_connect_gate;
            _gate_call_client.onconnect_hub_sucess += on_ack_connect_hub;
            _gate_call_client.onack_heartbeats += on_ack_heartbeats;
            _gate_call_client.oncall_client += on_call_client;
			_process.reg_module(_gate_call_client);
			_conn = new service.connectnetworkservice(_process);
            _conn.onChannelDisconnect += on_disconnect;

            _juggleservice = new service.juggleservice();
            _juggleservice.add_process(_process);

            _heartbeats = 0;
            _is_enable_heartbeats = false;
            connect_state = false;
        }

        private void on_disconnect(juggle.Ichannel ch)
        {
            if (ch != tcp_ch)
            {
                return;
            }

            if (!connect_state)
            {
                return;
            }

            log.log.error(new System.Diagnostics.StackFrame(), service.timerservice.Tick, "on_disconnect");

            connect_state = false;

            if (onDisConnect != null)
            {
                onDisConnect();
            }
        }

        private void heartbeats(Int64 tick)
        {
            do
            {
                if (!connect_state)
                {
                    break;
                }

                if (_is_enable_heartbeats && (_heartbeats < (tick - 20 * 1000)))
                {
                    log.log.error(new System.Diagnostics.StackFrame(), tick, "heartbeats:{0}", _heartbeats);

                    connect_state = false;

                    if (onDisConnect != null)
                    {
                        onDisConnect();
                    }

                    break;
                }

                _client_call_gate.heartbeats(tick);

            } while (false);

            timer.addticktime(5 * 1000, heartbeats);
        }

        private void on_ack_heartbeats()
        {
            _heartbeats = service.timerservice.Tick;
        }

        public delegate void onConnectGateHandle();
		public event onConnectGateHandle onConnectGate;
		private void on_ack_connect_gate()
		{
            _heartbeats = service.timerservice.Tick;
            _client_call_gate.heartbeats(service.timerservice.Tick);

            if (!is_reconnect)
            {
                timer.addticktime(5 * 1000, heartbeats);
            }

            if (onConnectGate != null)
			{
                onConnectGate();
            }

            connect_state = true;
        }

        public delegate void onConnectHubHandle(string _hub_name);
        public event onConnectHubHandle onConnectHub;
        private void on_ack_connect_hub(string _hub_name)
        {
            if (onConnectHub != null)
            {
                onConnectHub(_hub_name);
            }
        }

        private void on_call_client(String module_name, String func_name, ArrayList argvs)
		{
			modulemanager.process_module_mothed(module_name, func_name, argvs);
		}

        public bool reconnect_server(String tcp_ip, short tcp_port)
        {
            try
            {
                tcp_ch.disconnect();

                log.log.operation(new System.Diagnostics.StackFrame(), service.timerservice.Tick, "uuid:{0}", uuid);
                uuid = System.Guid.NewGuid().ToString();
                log.log.operation(new System.Diagnostics.StackFrame(), service.timerservice.Tick, "uuid:{0}", uuid);
                is_reconnect = true;

                tcp_ch = _conn.connect(tcp_ip, tcp_port);
                _client_call_gate = new caller.client_call_gate(tcp_ch);
                _client_call_gate.connect_server(uuid, service.timerservice.Tick);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

		public bool connect_server(String tcp_ip, short tcp_port)
		{
			try
			{
                is_reconnect = false;

                tcp_ch = _conn.connect(tcp_ip, tcp_port);
				_client_call_gate = new caller.client_call_gate(tcp_ch);
				_client_call_gate.connect_server(uuid, service.timerservice.Tick);
            }
			catch (Exception)
			{
				return false;
			}

			return true;
		}

		public void cancle_server()
		{
			_client_call_gate.cancle_server();
		}

        public void enable_heartbeats()
        {
            _client_call_gate.enable_heartbeats();

            _is_enable_heartbeats = true;
            _heartbeats = service.timerservice.Tick;
        }

        public void disable_heartbeats()
        {
            _client_call_gate.disable_heartbeats();

            _is_enable_heartbeats = false;
        }

        public void connect_hub(string hub_name)
        {
            _client_call_gate.connect_hub(uuid, hub_name);
        }

        public void disconnect_hub(string hub_name)
        {
            _client_call_gate.disconnect_hub(uuid, hub_name);
        }

        public void call_hub(String hub_name, String module_name, String func_name, params object[] _argvs)
        {
            ArrayList _argvs_list = new ArrayList();
            foreach (var o in _argvs)
            {
                _argvs_list.Add(o);
            }

            _client_call_gate.forward_client_call_hub(hub_name, module_name, func_name, _argvs_list);
        }

        public Int64 poll()
        {
            Int64 tick = timer.poll();
            _juggleservice.poll(tick);

            System.GC.Collect();

            return tick;
        }

        private static void Main()
        {
            client _client = new client();

            Int64 old_tick = 0;
            Int64 tick = 0;
            while (true)
            {
                old_tick = tick;
                tick = _client.poll();
                
                Int64 ticktime = (tick - old_tick);
                if (ticktime < 100)
                {
                    Thread.Sleep(15);
                }
            }
        }

        public String uuid;
		public service.timerservice timer;
		public common.modulemanager modulemanager;

        private Int64 _heartbeats;
        private bool _is_enable_heartbeats;

        private service.connectnetworkservice _conn;
        private juggle.Ichannel tcp_ch;
		private module.gate_call_client _gate_call_client;
		private caller.client_call_gate _client_call_gate;

        private bool connect_state;
        private bool is_reconnect;

        private service.juggleservice _juggleservice;

    }
}

