﻿using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace MyPhone.OBEX
{
    public class ObexClient
    {
        private DataReader _reader;
        private DataWriter _writer;

        public bool Conntected { get; private set; } = false;

        public ObexClient(IInputStream inputStream, IOutputStream outputStream)
        {
            _reader = new DataReader(inputStream);
            _writer = new DataWriter(outputStream);
        }

        /// <summary>
        /// Send OBEX Connect packet to the server.
        /// </summary>
        /// <param name="targetUuid">A 16-length byte array indicates the UUID of the target service.</param>
        /// <exception cref="InvalidOperationException">The Connect method can call only once and it is already called before.</exception>
        /// <exception cref="ObexExceptions">The request failed due to an underlying issue such as connection issue, or the server reply with a invalid response</exception>
        public async Task Connect(ObexServiceUuid targetService)
        {
            if (Conntected)
            {
                throw new InvalidOperationException("ObexClient is already connected to a ObexServer");
            }

            ObexConnectPacket packet = new ObexConnectPacket(targetService);
            var buf = packet.ToBuffer();

            Console.WriteLine("Sending OBEX Connection request to server:");
            Console.WriteLine(BitConverter.ToString(buf.ToArray()));
            Console.WriteLine("Opcode: " + packet.Opcode);

            _writer.WriteBuffer(buf);
            await _writer.StoreAsync();

            Console.WriteLine("Waiting reply packet...");
            ObexPacket response = await ObexPacket.ReadFromStream(_reader, packet);

            var bytes = response.ToBuffer().ToArray();
            Console.WriteLine("Reply packet:");
            Console.WriteLine(BitConverter.ToString(bytes));
            Console.WriteLine($"ResponseCode: {response.Opcode}");
            response.PrintHeaders();

            if (response.Opcode.ObexOperation != ObexOperation.Success)
            {
                throw new ObexRequestException(response.Opcode, $"Unable to connect to the target OBEX service.");
            }

            Conntected = true;
            OnConnected(response);
        }

        protected virtual void OnConnected(ObexPacket connectionResponse) { }

        public Task Disconnect()
        {
            if (!Conntected)
            {
                throw new InvalidOperationException("ObexClient is not connected to any ObexServer");
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Send OBEX request to MSE
        /// </summary>
        /// <param name="req">The request packet</param>
        /// <returns>Response packet. The resposne packet is null if the MSE did not send back any response, or the response is corrupted</returns>
        /// <exception cref="ObexRequestException">Throws if get an valid response, but its opcode is unsuccessful</exception>
        /// <exception cref="ObexException"> due to an underlying issue such as connection loss, invalid server response</exception>
        public async Task<ObexPacket> RunObexRequest(ObexPacket req)
        {
            if (!Conntected)
            {
                throw new InvalidOperationException("ObexClient is not connected to any ObexServer");
            }

            ObexOperation? requestOperation = req.Opcode.ObexOperation;
            if (requestOperation == null)
            {
                throw new InvalidOperationException("User-defined opcode is not supported");
            }

            ObexPacket? response = null;
            int c = 0;

            do
            {
                Console.WriteLine($"Sending request packet: {++c}");
                var buf = req.ToBuffer();
#if DEBUG
                Console.WriteLine(BitConverter.ToString(buf.ToArray()));
                Console.WriteLine("Opcode: " + req.Opcode);
#endif
                _writer.WriteBuffer(buf);
                await _writer.StoreAsync();

                ObexPacket subResponse;
                subResponse = await ObexPacket.ReadFromStream(_reader);
#if DEBUG
                var bytes = subResponse.ToBuffer().ToArray();
                Console.WriteLine("Reply packet:");
                Console.WriteLine(BitConverter.ToString(bytes));
                Console.WriteLine($"ResponseCode: {subResponse.Opcode}");
                subResponse.PrintHeaders();
#endif

                if (response == null)
                {
                    response = subResponse;
                }

                switch (subResponse.Opcode.ObexOperation)
                {
                    case ObexOperation.Success:
                        if (subResponse.Headers.ContainsKey(HeaderId.EndOfBody))
                        {
                            if (response.Headers.ContainsKey(HeaderId.Body))
                            {
                                ((BodyHeader)response.Headers[HeaderId.Body]).Value += ((BodyHeader)subResponse.Headers[HeaderId.EndOfBody]).Value;
                            }
                            else
                            {
                                response.Headers[HeaderId.Body] = response.Headers[HeaderId.EndOfBody];
                            }
                        }
                        response.Opcode = subResponse.Opcode;
                        return response;
                    case ObexOperation.Continue:
                        if (response != subResponse)
                        {
                            if (response.Headers.ContainsKey(HeaderId.Body))
                            {
                                ((BodyHeader)response.Headers[HeaderId.Body]).Value += ((BodyHeader)subResponse.Headers[HeaderId.Body]).Value;
                            }
                            else
                            {
                                if (subResponse.Headers.ContainsKey(HeaderId.Body))
                                {
                                    response.Headers[HeaderId.Body] = subResponse.Headers[HeaderId.Body];
                                }
                            }
                        }
                        break;
                    default:
                        throw new ObexRequestException(subResponse.Opcode, $"The {requestOperation} request failed with opcode {subResponse.Opcode}");
                }

                req = new ObexPacket(new ObexOpcode(requestOperation.Value, true));
            } while (true);
        }
    }
}
