# Limitations

HerdKV is intentionally small and local.

It is not:

- a SQL database
- a query engine
- a Unity object serializer
- a cloud save service
- a multi-process database

Use one process per database path. Store your own serialized payloads when saving complex objects.
