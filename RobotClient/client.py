from datetime import datetime
import enum
import glob
import os
import signal
import socket
import struct
import sys
import tempfile

class AgentRequestType(enum.IntEnum):
    REGISTRATION = 0
    REQUEST_FOR_WORK = 1
    POSITION_UPDATE = 2
    TASK_COMPLETE = 3
    REQUEST_FOR_CAMERA_DATA = 4
    DEREGISTRATION = 5

class DataRequestStatusCode(enum.IntEnum):
    SUCCESS = 0
    FAILURE_AGENT_TOO_FAR = 1
    FAILURE_NO_TASKS = 2
    FAILURE_REQUEST_PARSING_ERROR = 3
    FAILURE_OTHER = 4

def recv_msg(sock):
    # Read message length and unpack it into an integer
    raw_msglen = recvall(sock, 4)
    if not raw_msglen:
        return None
    msglen = struct.unpack('!I', raw_msglen)[0]
    print('Got subsequent message length of %s' % msglen)
    # Read the message data (minus what was already read)
    return recvall(sock, msglen-4)

def recvall(sock, n):
    # Helper function to recv n bytes or return None if EOF is hit
    data = bytearray()
    while len(data) < n:
        byte_count_this_recv = n - len(data)
        print('Receiving %s bytes' % byte_count_this_recv)
        packet = sock.recv(byte_count_this_recv)
        if not packet:
            return None
        data.extend(packet)
    return data

def parse_response_packet(data):
    HEADER_SIZE = 14 - 4 # Already parsed 4 bytes when we read the packet size

    # Check length
    if len(data) < HEADER_SIZE:
        print('Error: got too-short response packet of length %s' % len(data))
        return False, [0, 0, 0, 0]

    # Decode header
    status_code_byte, sensor_type_byte, response_id, payload_size = struct.unpack('!BBII', data[:HEADER_SIZE])

    # Check length of payload
    payload = data[HEADER_SIZE:]
    if len(payload) != payload_size:
        print('Error: declared payload size (%s) doesn\'t match observed size (%s)' % (payload_size, len(payload)))
        return False, [0, 0, 0, 0]

    # Check header components
    status_code = int(status_code_byte)
    sensor_type = int(sensor_type_byte)

    print('status_code (%s) unpacked' % status_code_byte)
    print('sensor_type (%s) unpacked' % sensor_type)
    print('response_id (%s) unpacked' % response_id)
    print('payload_size (%s) unpacked' % payload_size)


    if (status_code >= len(DataRequestStatusCode)):
        print('Error: status_code (%s) not recognized' % status_code_byte)
        return False, [0, 0, 0, 0]
                
    if (sensor_type >= len(DataRequestSensorType)):
        print('Error: sensor_type (%s) not recognized' % sensor_type)
        return False, [0, 0, 0, 0]
    
    # If we've gotten here, everything should be correct, so return the parsed data
    return True, (DataRequestStatusCode(status_code), DataRequestSensorType(sensor_type), response_id, payload)

def test_connect_and_send_request(ip, port):
    
    # Parse response
    parse_success, (status_code, sensor_type, response_id, payload) = parse_response_packet(response_data)

    print('parse_success: %s' % parse_success)
    print('status_code: %s' % status_code)
    print('sensor_type: %s' % sensor_type)
    print('response_id: %s' % response_id)
    print('payload length: %s' % len(payload))

    # Write out payload
    if parse_success and (status_code == DataRequestStatusCode.SUCCESS):
        time_as_str = datetime.now().strftime('%H_%M_%S_%f')
        save_payload(sensor_type, payload, sensor_as_string + '_dist_%s_m.' % dist + time_as_str)
    else:
        print('Could not save payload due to error.')
        print('Status code of %s means:' % status_code)
        print(get_message_for_status_code(status_code))

def connect_and_send_request(ip, port, request_data):
    # Create a client socket
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    client_socket.settimeout(10)

    # Connect to the server
    client_socket.connect((ip, port))

    # Send request data to server
    print('Sending request to server (%s bytes)' % len(request_data))
    client_socket.send(request_data)

    # Receive response data from server
    response_data = recv_msg(client_socket)

    # Print to the console
    print('Received response from server (%s bytes)' % len(response_data))

    client_socket.close()

    return response_data

def make_registration_packet(my_color_rgb_float, my_preferred_name):
    # First byte is registration type
    # Next 12 bytes are color
    # Next 16 bytes are name (right-padded with spaces)

    if len(my_preferred_name) > 16:
        # Trim name to 16 characters
        print('Trimming name %s due to it being over 16 characters...' % my_preferred_name)
        my_preferred_name = my_preferred_name[:16]
        print('New name is %s' % my_preferred_name)

    # Now, pad to 16 characters
    my_preferred_name = my_preferred_name.ljust(16, ' ')

    return struct.pack('!B3f16sxxx', AgentRequestType.REGISTRATION, my_color_rgb_float[0], my_color_rgb_float[1], my_color_rgb_float[2], str.encode(my_preferred_name))

def test_connect_and_register(ip, port):
    bytes_to_send = make_registration_packet((0.0, 0.0, 1.0), 'TestAgent')
    connect_and_send_request(ip, port, bytes_to_send)

def signal_handler(sig, frame):
    print('You pressed Ctrl+C')
    sys.exit(0)

def main():
    signal.signal(signal.SIGINT, signal_handler)

    if len(sys.argv) != 3:
        print('Please specify an IP address (first argument) and a port (second argument)')
    else:
        test_connect_and_register(sys.argv[1], int(sys.argv[2]))

if __name__ == '__main__':
    main()