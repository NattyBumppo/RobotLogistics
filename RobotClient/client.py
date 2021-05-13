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
    REQUEST_FOR_TASK = 1
    POSITION_UPDATE = 2
    TASK_COMPLETE = 3
    DEREGISTRATION = 4
    STATUS_UPDATE = 5

class DataRequestStatusCode(enum.IntEnum):
    SUCCESS = 0
    FAILURE_NO_TASKS = 1
    FAILURE_REQUEST_PARSING_ERROR = 2
    FAILURE_OTHER = 3

def recv_msg(sock):
    # Read message length and unpack it into an integer
    raw_msglen = recvall(sock, 4)
    if not raw_msglen:
        return None
    msglen = struct.unpack('!I', raw_msglen)[0]
    print('Got subsequent message length of %s' % msglen)
    # Read the message data (minus what was already read)
    return recvall(sock, msglen)

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

def parse_registration_response(data):
    # Get type of response
    response_type = DataRequestStatusCode(data[0])

    if response_type != DataRequestStatusCode.SUCCESS:
        print('Error: received failure from server as response to registration.')

        return False, None, None

    # Grab my graph index
    my_graph_index = struct.unpack('!I', data[1:5])[0]
    print('Got graph index of %s' % my_graph_index)

    map_string = data[5:].decode('ascii').strip()
    
    my_map = pathfinding.parse_map_data_and_populate_map(map_string, my_graph_index)

    return True, my_graph_index, my_map

def parse_task_request_response(data):
    # Get type of response
    response_type = DataRequestStatusCode(data[0])

    if response_type != DataRequestStatusCode.SUCCESS:
        print('Error: received failure from server as response to work request.')

        return response_type, None, None

    # Grab name of task
    task_name = data[1:33].decode('ascii').strip()

    # Grab graph idx for destination node
    print(len(data))
    destination_graph_index = struct.unpack('!I', data[33:37])[0]

    print('Parsed a successful response to work request.')
    print('Got a task of %s and destination of %s.' % (task_name, destination_graph_index))

    return response_type, task_name, destination_graph_index
   

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

    # Pad to 64 bytes for consistent packet size
    return struct.pack('!BIIf16s25x', AgentRequestType.POSITION_UPDATE, start_node_graph_idx, end_node_graph_idx, fraction_travelled, my_preferred_name_bytes)

def make_registration_packet(my_color_rgb_float, my_preferred_name):
    my_preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    # Pad to 64 bytes for consistent packet size
    return struct.pack('!B3f16s35x', AgentRequestType.REGISTRATION, my_color_rgb_float[0], my_color_rgb_float[1], my_color_rgb_float[2], my_preferred_name_bytes)

def make_task_complete_packet(my_preferred_name):
    # First byte is packet type
    # Next 16 bytes are name (right-padded with spaces)
    my_preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    # Pad to 64 bytes for consistent packet size
    return struct.pack('!B16s47x', AgentRequestType.TASK_COMPLETE, my_preferred_name_bytes) 

def make_deregistration_packet(my_preferred_name):
    # First byte is packet type
    # Next 16 bytes are name (right-padded with spaces)
    my_preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    # Pad to 64 bytes for consistent packet size
    return struct.pack('!B16s47x', AgentRequestType.DEREGISTRATION, my_preferred_name_bytes)

def make_request_for_task_packet(my_preferred_name):
    # First byte is packet type
    # Next 16 bytes are name (right-padded with spaces)
    my_preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    # Pad to 64 bytes for consistent packet size
    return struct.pack('!B16s47x', AgentRequestType.REQUEST_FOR_TASK, my_preferred_name_bytes)

def make_status_update_packet(my_status, my_preferred_name):
    # First byte is packet type
    # Next 31 bytes are status (right-padded with spaces)
    my_status_bytes = trim_and_pad_string_then_convert_to_bytes(my_status, 31)

    # Next 16 bytes are preferred name (right-padded with spaces)
    preferred_name_bytes = trim_and_pad_string_then_convert_to_bytes(my_preferred_name, 16)

    # Pad to 64 bytes for consistent packet size
    return struct.pack('!B31s16s16x', AgentRequestType.STATUS_UPDATE, my_status_bytes, preferred_name_bytes)

def connect_and_deregister(ip, port, chosen_name):
    bytes_to_send = make_deregistration_packet(chosen_name)
    connect_and_send_request(ip, port, bytes_to_send)

def connect_and_send_task_complete_notification(ip, port, chosen_name):
    bytes_to_send = make_task_complete_packet(chosen_name)
    connect_and_send_request(ip, port, bytes_to_send)

def connect_and_update_status(ip, port, my_status, chosen_name):
    print('Updating status to ' + my_status)

    bytes_to_send = make_status_update_packet(my_status, chosen_name)

    print('Made packet with %s bytes' % len(bytes_to_send))

    connect_and_send_request(ip, port, bytes_to_send)

def connect_and_update_position(ip, port, fraction, my_preferred_name, start_node_graph_idx, end_node_graph_idx):
    bytes_to_send = make_position_update_packet(start_node_graph_idx, end_node_graph_idx, fraction, my_preferred_name)
    connect_and_send_request(ip, port, bytes_to_send)

def signal_handler(sig, frame):
    print('You pressed Ctrl+C')
    sys.exit(0)

def navigate(server_ip, server_port, my_map, start_idx, end_idx, chosen_name, running_interval, status_msg):
    path = pathfinding.get_path(my_map.node_list[start_idx], my_map.node_list[end_idx])

    print('Here\'s the path we got from', my_map.node_list[start_idx].graph_idx, 'to', my_map.node_list[end_idx].graph_idx)

    # Unpack path
    path_as_list = []
    for node in path:
        path_as_list.append(node)

    # Update my status
    connect_and_update_status(server_ip, server_port, status_msg, chosen_name)

    # Follow and animate path
    for i in range(len(path_as_list)-1):
        start_idx = path_as_list[i].graph_idx
        end_idx = path_as_list[i+1].graph_idx
        
        for j in range(5):
            connect_and_update_position(server_ip, server_port, float(j) / 5.0, chosen_name, start_idx, end_idx)
            time.sleep(running_interval)

    # Set to goal
    connect_and_update_position(server_ip, server_port, 1.0, chosen_name, path_as_list[-2].graph_idx, path_as_list[-1].graph_idx)

def run(ip, port, robot_type, chosen_name=''):
    robot_type = robot_type.lower()

    if robot_type == 'loader':
        robot_color = (1.0, 0.0, 0.0)
        name_prefix = 'L'
        loading_interval = 0.1
        running_interval = 0.2
    elif robot_type == 'runner':
        robot_color = (0.0, 0.0, 1.0)
        name_prefix = 'R'
        loading_interval = 0.2
        running_interval = 0.1
    else:
        print('Error: unknown robot type %s' % robot_type)
        return

    if chosen_name == '':
        chosen_name = name_prefix + random.choice(string.digits) + random.choice(string.digits) + random.choice(string.digits) + random.choice(string.digits)
    bytes_to_send = make_registration_packet(robot_color, chosen_name)
    response = connect_and_send_request(ip, port, bytes_to_send)
    success, initial_start_idx, my_map = parse_registration_response(response)

    if not success:
        print('Error with registration, deregistering and exiting.')
        return

    # Go from start index to HQ
    navigate(ip, port, my_map, initial_start_idx, my_map.hq_node.graph_idx, chosen_name, running_interval, '(Back to HQ)')

    while True:

        # Update status
        my_status = '(Waiting for task)'
        connect_and_update_status(ip, port, my_status, chosen_name)

        waiting_for_task = True
        
        while waiting_for_task:
            # Send a request for a task
            bytes_to_send = make_request_for_task_packet(chosen_name)
            response = connect_and_send_request(ip, port, bytes_to_send)
            response_type, task_name, destination_graph_index = parse_task_request_response(response)

            print('Response type:', str(response_type))

            if response_type == DataRequestStatusCode.FAILURE_NO_TASKS:
                print('No tasks available!')
        
                my_status = '(Waiting for task)'
                connect_and_update_status(ip, port, my_status, chosen_name)

                # Wait for more tasks to become available
                time.sleep(0.5)
            elif response_type == DataRequestStatusCode.SUCCESS:
                # Break out of loop
                waiting_for_task = False
            else: # Some other error occurred
                print('Error with task request, exiting.')
                connect_and_deregister(ip, port, chosen_name)
                return

        # Update status to load item
        my_status = '(Loading %s)' % task_name
        connect_and_update_status(ip, port, my_status, chosen_name)

        # Perform load
        for i in range(20):
            time.sleep(loading_interval)

        # Go from HQ to delivery location
        navigate(ip, port, my_map, my_map.hq_node.graph_idx, destination_graph_index, chosen_name, running_interval, '(Delivering %s)' % task_name)

        # Perform delivery
        my_status = '(Handing off %s)' % task_name
        connect_and_update_status(ip, port, my_status, chosen_name)    
        
        for i in range(20):
            time.sleep(loading_interval)

        # Inform server that task is complete
        connect_and_send_task_complete_notification(ip, port, chosen_name)

        # Go from start index to HQ
        navigate(ip, port, my_map, destination_graph_index, my_map.hq_node.graph_idx, chosen_name, running_interval, '(Back to HQ)')

    connect_and_deregister(ip, port, chosen_name)

def main():
    signal.signal(signal.SIGINT, signal_handler)

    if len(sys.argv) != 4 and len(sys.argv) != 5:
        print('Please specify an IP address (first argument) and a port (second argument) and a robot type (third argument, either LOADER or RUNNER) and a name (optional)')
    elif len(sys.argv) == 4:
        run(sys.argv[1], int(sys.argv[2]), sys.argv[3])
    else:
        run(sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4])


if __name__ == '__main__':
    main()