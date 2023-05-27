#!/usr/bin/env bash

REPO=$(cat repo.txt)

echo Building...
docker build -t httpcache -f Dockerfile .

if [[ ! -z $REPO ]]; then
    echo Pushing to repo: $REPO
    docker tag httpcache:latest $REPO:latest
    docker push $REPO:latest
fi