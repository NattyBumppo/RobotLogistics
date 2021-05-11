from datetime import datetime
import enum
import random
import signal
import socket
import string
import struct
import sys
import time

import pathfinding

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
    print('recvall() for %s bytes' % n)

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
        # time_as_str = datetime.now().strftime('%H_%M_%S_%f')
        # save_payload(sensor_type, payload, sensor_as_string + '_dist_%s_m.' % dist + time_as_str)
        print('Success!')
    else:
        print('Could not save payload due to error.')
        print('Status code of %s means:' % status_code)
        print(get_message_for_status_code(status_code))

def parse_registration_response(data):
    # Get type of response
    response_type = DataRequestStatusCode(data[0])

    # Grab my graph index
    graph_index = struct.unpack('!I', data[1:5])
    print('Got graph index of %s' % graph_index)

    map_string = data[5:].decode('ascii')
    
    my_map = pathfinding.parse_map_data_and_populate_map(map_string, graph_index)

    # print(my_map.node_list[3])
    # print(my_map.node_list[10])

    return my_map



    # for i in range(5):
    #     test_connect_and_update_position(sys.argv[1], int(sys.argv[2]), float(i) / 5.0, chosen_name)
    #     time.sleep(0.1)


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

def trim_and_pad_string_then_convert_to_bytes(string_to_convert, char_limit):
    if len(string_to_convert) > char_limit:
        # Trim name to char_limit characters
        print('Trimming name %s due to it being over %s characters...' % (string_to_convert, char_limit))
        string_to_convert = string_to_convert[:char_limit]
        print('New name is %s' % string_to_convert)

    # Now, pad to char_limit characters
    string_to_convert = string_to_convert.ljust(char_limit, ' ')

    return str.encode(string_to_convert)

def make_position_update_packet(start_node_graph_idx, end_node_graph_idx, fraction_travelled, my_preferred_name):

    print('Requesting to move agent to fraction ' + str(fraction_travelled) + ' between ' + str(start_node_graph_idx) + ' and ' + str(end_node_graph_idx))


    my_preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    return struct.pack('!BIIf16sxxx', AgentRequestType.POSITION_UPDATE, start_node_graph_idx, end_node_graph_idx, fraction_travelled, my_preferred_name_bytes)

def make_registration_packet(my_color_rgb_float, my_preferred_name):
    my_preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    return struct.pack('!B3f16sxxx', AgentRequestType.REGISTRATION, my_color_rgb_float[0], my_color_rgb_float[1], my_color_rgb_float[2], my_preferred_name_bytes)

def make_deregistration_packet(my_preferred_name):
    # First byte is registration type
    # Next 16 bytes are name (right-padded with spaces)
    my_preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    return struct.pack('!B16s15x', AgentRequestType.DEREGISTRATION, my_preferred_name_bytes)

def test_connect_and_register(ip, port, start_idx, goal_idx):
    chosen_name = random.choice(string.ascii_uppercase) + random.choice(string.ascii_uppercase) + random.choice(string.ascii_uppercase)
    bytes_to_send = make_registration_packet((0.0, 0.0, 1.0), chosen_name)
    response = connect_and_send_request(ip, port, bytes_to_send)
    my_map = parse_registration_response(response)

    path = pathfinding.get_path(my_map.node_list[start_idx], my_map.node_list[goal_idx])

    print('Here\'s the path we got from', my_map.node_list[start_idx].graph_idx, 'to', my_map.node_list[goal_idx].graph_idx)

    # Unpack path
    path_as_list = []
    for node in path:
        path_as_list.append(node)

    # Follow and animate path
    for i in range(len(path_as_list)-1):
        start_node_graph_idx = path_as_list[i].graph_idx
        end_node_graph_idx = path_as_list[i+1].graph_idx
        
        for j in range(4):
            test_connect_and_update_position(sys.argv[1], int(sys.argv[2]), float(j) / 5.0, chosen_name, start_node_graph_idx, end_node_graph_idx)
            time.sleep(0.1)

    # Set to goal
    test_connect_and_update_position(sys.argv[1], int(sys.argv[2]), 1.0, chosen_name, path_as_list[-2].graph_idx, path_as_list[-1].graph_idx)

    return chosen_name

def test_connect_and_deregister(ip, port, chosen_name):
    bytes_to_send = make_deregistration_packet(chosen_name)
    connect_and_send_request(ip, port, bytes_to_send)

def test_connect_and_update_position(ip, port, fraction, my_preferred_name, start_node_graph_idx, end_node_graph_idx):
    bytes_to_send = make_position_update_packet(start_node_graph_idx, end_node_graph_idx, fraction, my_preferred_name)
    connect_and_send_request(ip, port, bytes_to_send)

def signal_handler(sig, frame):
    print('You pressed Ctrl+C')
    sys.exit(0)

def main():
    signal.signal(signal.SIGINT, signal_handler)

    if len(sys.argv) != 5:
        print('Please specify an IP address (first argument) and a port (second argument) and a start node and an end node')
    else:
        chosen_name = test_connect_and_register(sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]))
        # time.sleep(3)
        # for i in range(11):
            # test_connect_and_update_position(sys.argv[1], int(sys.argv[2]), float(i) / 10.0, chosen_name)
            # time.sleep(0.5)
        # time.sleep(2)
        # test_connect_and_deregister(sys.argv[1], int(sys.argv[2]), chosen_name)


if __name__ == '__main__':
    main()