#!/usr/bin/env bash
PORT=80
HOST=localhost
RANDOM_HEADER=$(echo $RANDOM)
echo "Using random value: $RANDOM_HEADER"

echo "Running curl for uncached request:"
time curl \
  --location "http://$HOST:$PORT" \
  --request GET \
  --header 'Actual-Host: http://google.com'

echo "Running curl for cached request:"
time curl \
  --location "http://$HOST:$PORT" \
  --request GET \
  --header 'Actual-Host: http://google.com'