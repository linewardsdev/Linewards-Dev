# Starts the BlenderMCP socket server the moment Blender launches, replacing the manual
# "N-panel > BlenderMCP > Connect to MCP server" click that every session used to need —
# and that blind GUI automation repeatedly fumbled (the N-toggle is stateless, so scripted
# keypresses toggle the sidebar without knowing which way).
#
# Launch Blender with:
#
#   open -a Blender --args --python tools/art/blender_mcp_autostart.py
#
# Then verify from the agent side with any mcp__blender__* call (get_scene_info is the
# cheapest); the socket listens on 127.0.0.1:9876. The timer delay exists because the
# addon registers during startup and the operator is not yet available when --python runs.
import bpy


def start():
    try:
        bpy.ops.blendermcp.start_server()
        print("BlenderMCP server started via autostart script")
    except Exception as exc:  # addon missing or renamed — say so in the console, loudly
        print("BlenderMCP autostart FAILED:", exc)
    return None  # unregister the timer


bpy.app.timers.register(start, first_interval=2.0)
