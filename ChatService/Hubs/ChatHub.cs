﻿using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Generic;
using ChatService;
using System.Linq;
using System;

namespace SignalRChat.Hubs
{
    public class ChatHub : Hub
    {
        
        private readonly string _botUser;
        private readonly IDictionary<string, UserConnection> _connections;
        
        private IDictionary<string, string[][]> _deck;
        private IDictionary<string, int[][]> _cleanDeck;
        private readonly Game _game;


        public ChatHub(IDictionary<string, UserConnection> connections, Game game)
        {
            _botUser = "MyChat Bot";
            _connections = connections;
            _game = game;

        }

       

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                _connections.Remove(Context.ConnectionId);
                Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User} has left");
                SendUsersConnected(userConnection.Room);
            }

            return base.OnDisconnectedAsync(exception);
        }

        public async Task JoinRoom(UserConnection userConnection)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userConnection.Room);

            _connections[Context.ConnectionId] = userConnection;
            userConnection.SignalrId = Context.ConnectionId;

            await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User} has joined {userConnection.Room}");

            await SendUsersConnected(userConnection.Room);
            
        }

        public async Task SendMessage(string message)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", userConnection.User, message);
            }
        }

        public Task SendUsersConnected(string room)
        {
            var users = _connections.Values
                .Where(c => c.Room == room)
                .Select(c => c.User);

            return Clients.Group(room).SendAsync("UsersInRoom", users);
        }

        public async Task SetPlayer (int player)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                userConnection.Player = player;

                await Clients.Group(userConnection.Room).SendAsync("ReceivePlayerNumber", userConnection.User, userConnection.Player);
            }
        }
        // ACCIÓN DE DAR CARTAS
        public async Task SendCards(string[][] deck)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                var clients = _connections.Values.Where(r => r.Room == userConnection.Room);
                var clientIds = clients
                .Select(c => c.ToString())
                .ToList();
                int position;
                position = userConnection.Player;

                for (int i = 0; i <4 ; i++)
                {
                    position++;
                    if(position == 4)
                    {
                        position = 0;
                    }
                     
                     string userActive = _connections.Values.Where(r => r.Room == userConnection.Room).Where(p => p.Player == position).Select(s=>s.SignalrId).FirstOrDefault();

                    await Clients.Client(userActive).SendAsync("ReceiveHandCards", deck[i]);
                }
                _cleanDeck["barajaLimpia"]=_game.CleanCards(deck);
            }


          
            


        }
        //Botón Ready. Cuando todos le dan el juego comienza y determina quien es el postre
        public async Task IsReady(bool ready)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                if (ready)
                {
                userConnection.Ready = ready;
                await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User} está listo");
                }
                else { await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User} se lo está pensando..."); }
               
                var readys= _connections.Values.Where(r=>r.Room == userConnection.Room).Select(re => re.Ready);
                int contador = 0;
                foreach (bool ok in readys)
                {
                    if (ok)
                    {
                        contador++;

                    if(contador == 4)
                        {
                            Random rnd = new Random();
                            int player3 = rnd.Next(0, 4);
                            var player= _connections.Values.Where(r => r.Room == userConnection.Room).Where(p => p.Player == player3).FirstOrDefault();

                            await Clients.Group(userConnection.Room).SendAsync("StartGame", player3);
                            await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{player.User} reparte cartas.");
                     
                        }
                    }
                } 
            
            }
        }
        //SETEAR PLAYERS EN NUEVAMANO

        public async Task NuevaMano()
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
              
                int nextPlayer= userConnection.Player;

                for (int i = 0; i < 4; i++)
                {
                    nextPlayer++;
                    if (nextPlayer == 4)
                    {
                        nextPlayer = 0;
                    }
                 var player = _connections.Values.Where(r => r.Room == userConnection.Room).Where(p => p.Player==nextPlayer).FirstOrDefault();
                 player.PosicionDeVuelta = i;
                }             
            }
        }
        // CAMBIAR TURNO
        public async Task ChangeTurn(int postre, int round)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {

                if (userConnection.Player == postre)//Si el que pasa es el postre pasamos de ronda.
                {
                    if(round == 3)
                    {
                        await Clients.Group(userConnection.Room).SendAsync("Accountant", round);
                    }
                    else
                    {
                        round++;
                        await Clients.Group(userConnection.Room).SendAsync("NextRound" , round);
                    }
                }
                else
                {
                    int turn = userConnection.Player;
                    turn++;
                    if (turn == 4)
                    {
                        turn = 0;
                    }                
                    await Clients.Group(userConnection.Room).SendAsync("NewTurn", turn);

                }
               

            }
        }
        //ACCIÓN DE MUS, DESCARTES
        public async Task DropCards(string[] dropped, int postre)
        {

            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {

                string userActive = _connections.Values.Where(r => r.Room == userConnection.Room).Where(p => p.Player == postre).Select(s => s.SignalrId).FirstOrDefault();
                int descartes = dropped.Count(c => c == "F000");
                int pide = 4 - descartes;

                await Clients.Client(userActive).SendAsync("DroppedCards", dropped);
                await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User} pide {descartes} cartas.");
                // await Clients.Group(userConnection.Room).SendAsync("Descarte", pide);
                
                if(userConnection.Player != postre)              
                {
                    int turn = userConnection.Player;
                    turn++;
                        if (turn == 4)
                        {
                            turn = 0;
                        }
                    await Clients.Group(userConnection.Room).SendAsync("NewTurn", turn);
                }


            }
        }



        //ACCIÓN NO HAY MUS

        public async Task NoMus(string[][]deck)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                
                    
                await Clients.Group(userConnection.Room).SendAsync("NoMus");
                _deck["baraja"] = deck;
                _cleanDeck["barajaLimpia"] = _game.CleanCards(_deck["baraja"]);


            }
        }

        //ACCIÓN ENVIDO

        public async Task Bet(int bet)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                string team;
                if (userConnection.Player%2 == 0)
                {
                    team = "blue";
                    await Clients.Group(userConnection.Room).SendAsync("Bet", bet, team);
                    await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User} envida {bet}");
                }
                else
                {
                    team = "red";
                    await Clients.Group(userConnection.Room).SendAsync("Bet", bet, team);
                    await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User}  envida {bet}.");
                }
            }
        }

        //ACCION NO QUIERO

        public async Task Fold(int contador)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                string team;
                if (userConnection.Player % 2 == 0)
                {
                    team = "red";
                    await Clients.Group(userConnection.Room).SendAsync("Fold", contador, team);
                }
                else
                {
                    team = "blue";
                    await Clients.Group(userConnection.Room).SendAsync("Fold", contador, team);
                }
                
            }
        }


        //QUIERO


        public async Task Call(int bet, int contador)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                if (contador == 1)
                {
                    contador = bet;
                    await Clients.Group(userConnection.Room).SendAsync("Call", contador);

                }
                else
                {

                    contador = contador + bet;
                
                   await Clients.Group(userConnection.Room).SendAsync("Call", contador);                             
                }
            }

        }

        //ACCION REENVIDO
        public async Task SecondBet(int contador, int bet, int secondBet)
        {
            contador = bet + contador;
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                string team;
                if (userConnection.Player % 2 == 0)
                {
                    team = "blue";
                    await Clients.Group(userConnection.Room).SendAsync("SecondBet", contador, secondBet, team);
                    await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User} sube {secondBet} piedras.");
                }
                else
                {
                    team = "red";
                    await Clients.Group(userConnection.Room).SendAsync("SecondBet", contador, secondBet, team);
                    await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"{userConnection.User}  sube {secondBet} piedras.");
                }
            }

        }

        //ACCION CONTARPIEDRAS
        //MAYOR (s.Remove(0, 1)

        public async Task AccountantMayor()
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                            
                int mayor = _game.Mayor(_cleanDeck["barajaClean"]);

                if (userConnection.Player % 2 == 0)
                {


                    if (mayor == 0)
                    {

                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Rojo gana la mayor.");

                    }
                    else
                    {
                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Azul gana la mayor.");
                    }
                }
                else
                {
                    if (mayor == 0)
                    {

                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Azul gana la mayor.");

                    }
                    else
                    {
                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Rojo gana la mayor.");
                    }
                }
 

            }
        }

        //Cuenta de PEQUEÑA
        public async Task AccountantPeque()
        {
            int peque = _game.Pequenia(_cleanDeck["barajaClean"]);
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {

                if (userConnection.Player % 2 == 0)
                {

                    if (peque == 1)
                    {
                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Rojo gana la pequeña.");
                    }
                    else
                    {
                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Azul gana la pequeña.");
                    }

                }
                else
                {
                    if (peque == 1)
                    {
                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Azul gana la pequeña.");
                    }
                    else
                    {
                        await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", _botUser, $"El equipo Rojo gana la pequeña.");
                    }
                }
            }
        }

        //HAY PARES
        public async Task HayPares(int postre)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                bool[] hayPares=new bool[4];
                hayPares = _game.HayPares(_cleanDeck["barajaClean"]);
                
                string[] hayParesString=new string[hayPares.Length];
                for (int i = 0; i < hayPares.Length; i++)
                {
                    if (hayPares[i])
                    {
                        hayParesString[i] = "Sí, tengo pares.";
                    }
                    else
                    {
                        hayParesString[i] = "no tengo pares.";
                    }
                }
                for(int i = 0;i < 4; i++)
                {
                    string userName = _connections.Values.Where(e => e.PosicionDeVuelta == i).Select(n => n.User).FirstOrDefault();
                    await Clients.Group(userConnection.Room).SendAsync("ReceiveMessage", userName, hayParesString);
                    System.Threading.Thread.Sleep(800);
                }
            }
        }


    }   
}
