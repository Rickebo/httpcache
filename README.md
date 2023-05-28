# httpcache

Simple HTTP caching proxy written in C# using ASP.NET 7.

## Setup

1. Install and set up Docker. 
2. Modify the settings in ``appsettings-example.json`` to your liking. See the following sections 
   for more information about this.
3. Navigate to the base directory of this repository in a terminal (i.e. where this file is located.) 
4. Run ``docker-compose up``. This will launch a docker container for the proxy, and a Redis 
   container to store the cache. It will also expose the proxy on port 80 on your machine. 

## Usage

Set up everything according the the setup instructions. Send any HTTP request to the proxy, and 
specify the URL you wish to forward the request to in the ``Actual-Host`` header. The proxy will
forward the request to the specified host, and cache the response. Subsequent requests to the same
URL will be served from the cache as long as the cache entry is valid, i.e. has not expired. 

Responses are cached in a Redis database. The cache key is the URL, method, body and headers of the 
request, where certain headers can be excluded from the cache key if desired. The cache entry is
valid for a certain amount of time, which can be specified in the configuration file.

For a simple example, see the [``test.sh``](test.sh) file.

## Settings

The settings are specified in the [``appsettings-example.json``](appsettings-example.json) file. The 
settings file is divided into different sections: ``Cache``, ``Http`` and ``Redis``, each of which
is explained in more detail below.

### Cache

Contains cache-specific settings that determine how and how long the proxy caches responses.

- ``DefaultMaxAge`` a time span specifying the default max cache time. If left undefined, this value is 
  determined by the cache backend, i.e. Redis.
- ``IgnoredHeaders`` a list of headers that are excluded when generating a cache key. This is useful for
  headers that are not relevant for the response, and that would otherwise cause unnecessary cache misses.

### Http

Contains HTTP-specific settings that determine how the proxy handles HTTP requests.

- ``HostHeader`` the name of the header that specifies where requests should be proxied to. Default is 
  ``Actual-Host``.
- ``Access`` a nested settings section specifying access control. See the following section "Access" for more 
  information.
- ``BlacklistedIp`` a list of IP address ranges that the proxy will not forward requests to. As long as any of
  the addresses a domain resolves to is blacklisted, it will not be proxied. This is useful for blocking
  for example local services that should not be exposed to the internet, if the proxy is exposed to the internet.
- ``BlacklistedHost`` a list of hostnames that the proxy will not forward requests to. 
- ``WhitelistedIp`` a list of IP address ranges that the proxy will forward requests to. If all of the IP addresses
  a domain resolves to is whitelisted, it will be proxied.
- ``WhitelistedHost`` a list of hostnames that the proxy will forward requests to.
- ``FilteredHeaders`` a list of headers excluded from the response to the client. Is useful to exclude headers that
  would otherwise interfere with the proxy's response, such as ``Transfer-Encoding``. Default value only excludes
  ``Transfer-Encoding``.

By default, ``BlacklistedIp`` contains the following address ranges:
- ``10.0.0.0/8``
- ``172.16.0.0/12``
- ``192.168.0.0/16``
- ``127.0.0.0/8``
- ``169.254.0.0/16``
- ``224.0.0.0/4``
- ``::1/128``
- ``::/128``
- ``fc00::/7``
- ``fe80::/10``

#### Access

Contains access control settings that determine how the proxy handles requests.

- ``BlacklistHosts`` boolean specifying whether or not the proxy should blacklist hosts specified in the "Http" 
  section. Default is ``false``.
- ``BlacklistIp`` boolean specifying whether or not the proxy should blacklist IP addresses specified in the "Http" 
  section. Default is ``true``.
- ``WhitelistHosts`` boolean specifying whether or not the proxy should whitelist only hosts specified in the "Http"
  section. Default is ``true``.
- ``WhitelistIp`` boolean specifying whether or not the proxy should whitelist only IP addresses specified in the "Http"
  section. Default is ``false``.

### Redis

Contains Redis-specific settings that determine how the proxy connects to Redis.

- ``EndPoints`` a list of endpoints the proxy should connect to. Which endpoint(s) is used is determined by the cache
  backend, i.e. Redis. 
