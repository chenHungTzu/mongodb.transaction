Start-Sleep -Seconds 10

docker exec -it mongo mongosh --eval "rs.initiate({ _id: 'mongo-replica-set', members: [{_id: 0, host: 'host.docker.internal'}]})"

Start-Sleep -Seconds 3

docker exec -it mongo mongosh --eval "db.createUser({user: 'root',pwd: 'example',roles: []})"
