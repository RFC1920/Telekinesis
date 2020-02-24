## Telekinesis

Current version 2.0.8: [Download](https://code.remod.org/Telekinesis.cs)

Allows players to move/rotate entities without picking them up.

### Notes

A revert point (undo point) is made every time you start the TLS tool in case you are unhappy with the results or something goes wrong.

Currently does not work best with foundations/other building parts that are attached to others, I recommend not using the tool on these.

### Usage

Use the left and right click to modify the distance/rotation (depending on what mode is enabled).
Use the reload key to switch modes (distance, horizontal rotation, vertical rotation, horizontal2 rotation, vertical offset).

### Permissions

This plugin uses Oxide's permission system. To assign a permission, use oxide.grant <user or group> <name or steam id> <permission>. To remove a permission, use oxide.revoke <user or group> <name or steam id> <permission>.

- `telekinesis.admin` - Allows players to use the /tls command without any restrictions
- `telekinesis.restricted` - Restricts players by settings you have set in the config (intended for use by players)

### Chat Commands

- `/tls` - Start/stop telekinesis on the entity you are looking at
- `/tls undo` - Revert the latest object back before the latest changes you made

### Configuration

The settings and options for this plugin can be configured in the Telekinesis.json file under the oxide/config directory. The use of a JSON editor or validation site such as jsonlint.com is recommended to avoid formatting issues and syntax errors.

- `"Restricted Cannot Use If Building Blocked"` - When this is turned on, players with the restricted permission will not be able to remove any objects from building blocked zones or travel within building blocked zones.
- `"Restricted Grab Distance"` - Controls how far restricted players are able to grab objects from.
- `"Restricted max distance"` - Controls how far restricted players are allowed to move objects away from them whilst controlling them.
- `"Restricted OwnerID Only"` - When this is turned on, restricted players will only be able to use telekinesis on objects which have been placed by them and have the same ownerID registered.

### Credits

- Bombardir, the original author of this plugin
- Fujikura, for the active item removal code
- redBDGR, long-time maintainr of this plugin
