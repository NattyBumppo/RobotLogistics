import astar
import math

class Map:
    def __init__(self, node_list, hq_node):
        self.node_list = node_list
        self.hq_node = hq_node

class Node:
    def __init__(self, graph_idx, pos):
        self.graph_idx = graph_idx
        self.pos = pos

        self.neighbor_nodes = []
        self.neighbor_indices_to_distances = {}

def parse_map_data_and_populate_map(map_data, my_graph_index):
    lines = map_data.split('\n')

    for i in range(5):
        print(i, ':', lines[i])

    # First, parse the HQ data (graphIdx and position)
    tokens = lines[0].strip().split(' ')
    hq_idx, hq_x, hq_y, hq_z = int(tokens[0]), float(tokens[1]), float(tokens[2]), float(tokens[3])

    # Now, parse the number of nodes
    num_nodes = int(lines[1])

    node_list = []

    # Now, parse one node at a time. Each node is formatted as
    # one line of node-specific data (graphIdx and position),
    # followed by one line about its neighbors
    for i in range(2, len(lines), 2):
        first_line_tokens = lines[i].strip().split(' ')
        graph_idx = int(first_line_tokens[0])
        pos_x, pos_y, pos_z = [float(coord) for coord in first_line_tokens[1:]]

        new_node = Node(graph_idx, (pos_x, pos_y, pos_z))

        node_list.append(new_node)

    # We parse the neighbor information in a second full iteration
    # through the lines so that we can refer to neighbors who've already been created
    for i in range(2, len(lines), 2):
        node_idx = int(i / 2 - 1)

        next_line_tokens = lines[i+1].strip().split(' ')

        # Go through and parse the neighbor line
        for j in range(0, len(next_line_tokens), 2):
            neighbor_idx = int(next_line_tokens[j])

            # Get the node corresponding to the aforementioned index
            neighbor_node = 0
            for n in node_list:
                if n.graph_idx == neighbor_idx:
                    neighbor_node = n

            node_list[node_idx].neighbor_nodes.append(neighbor_node)

            dist_to_neighbor = float(next_line_tokens[j+1])
            node_list[node_idx].neighbor_indices_to_distances[neighbor_idx] = dist_to_neighbor

    hq_node = Node(hq_idx, (hq_x, hq_y, hq_z))

    new_map = Map(node_list, hq_node)

    print('Generated new map with %s nodes' % len(node_list))

    if num_nodes != len(node_list):
        print('ERROR! Got a mismatch between the specified number of nodes and the number of nodes read.')

    return new_map


# Runs A* on the map
def get_path(n1, n2):

    # print('Getting path between %s and %s' % (n1, n2))

    def estimate_distance(n1, n2):
        """Computes the 'as the crow flies' distance between two nodes"""
        x1, y1, z1 = n1.pos
        x2, y2, z2 = n2.pos
        
        return math.sqrt((x2-x1)**2 + (y2-y1)**2 + (z2-z1)**2)

    def distance_between_neighbors(n1, n2):
        # print('Getting distance between %s and %s' % (n1, n2))
        return n1.neighbor_indices_to_distances[n2.graph_idx]

    return astar.find_path(n1, n2, neighbors_fnct=lambda n: n.neighbor_nodes, heuristic_cost_estimate_fnct=estimate_distance, distance_between_fnct=distance_between_neighbors)
