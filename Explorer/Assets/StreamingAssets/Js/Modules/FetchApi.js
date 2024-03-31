  
/*
/// --- FETCH ---

type RequestRedirect = 'follow' | 'error' | 'manual'
type ResponseType = 'basic' | 'cors' | 'default' | 'error' | 'opaque' | 'opaqueredirect'

interface RequestInit {
  // whatwg/fetch standard options
  body?: string
  headers?: { [index: string]: string }
  method?: string
  redirect?: RequestRedirect

  // custom DCL property
  timeout?: number
}

interface ReadOnlyHeaders {
  get(name: string): string | null
  has(name: string): boolean
  forEach(callbackfn: (value: string, key: string, parent: ReadOnlyHeaders) => void, thisArg?: any): void
}

interface Response {
  readonly headers: ReadOnlyHeaders
  readonly ok: boolean
  readonly redirected: boolean
  readonly status: number
  readonly statusText: string
  readonly type: ResponseType
  readonly url: string

  json(): Promise<any>
  text(): Promise<string>
}

declare function fetch(url: string, init?: RequestInit): Promise<Response>

*/

async function restrictedFetch(url, init) {
    const canUseFetch = true // TODO: this should come from somewhere

    if (url.toLowerCase().substr(0, 8) !== "https://") {
            return Promise.reject(new Error("Can't make an unsafe http request, please upgrade to https. url=" + url))
        }

    if (!canUseFetch) {
        return Promise.reject(new Error("This scene is not allowed to use fetch."))
    }

    return await fetch(url, init)
}

async function fetch(url, init) {
    const { body, headers, method, redirect, timeout } = init ?? {}
    const hasBody = typeof body === 'string'
    const reqMethod = method ?? 'GET'
    const reqTimeout = timeout ?? 30
    const reqHeaders = headers ?? {}
    const reqRedirect = redirect ?? 'follow'

    console.error("NOT ERROR: Starting FETCH:");

    const response = await UnitySimpleFetchApi.Fetch(
        reqMethod, url, reqHeaders, hasBody, body ?? '', reqRedirect, reqTimeout
    )
    
    console.error("NOT ERROR: Received REsponse to FETCH:");


    response.headers = new Headers(response.headers)
    // TODO: the headers object should be read-only

    let alreadyConsumed = false
    function notifyConsume() {
        if (alreadyConsumed) {
            throw new Error("Response body has already been consumed.")
        }
        alreadyConsumed = true
    }

    function throwErrorFailed() {
        if (response.type === "error") {
            throw new Error("Failed to fetch " + response.statusText)
        }
    }


    Object.assign(response, {
        async json() {
            notifyConsume()
            throwErrorFailed()
            return JSON.parse(response.data)
        },
        async text() {
            notifyConsume()
            throwErrorFailed()
            return data
        }
    })

    return response
}

class Headers {
    constructor(init = {}) {
        this.headers = {};

        if (init instanceof Headers) {
            init.forEach((value, name) => {
                this.append(name, value);
            });
        } else if (Array.isArray(init)) {
            init.forEach(([name, value]) => {
                this.append(name, value);
            });
        } else if (init && typeof init === 'object') {
            Object.keys(init).forEach(name => {
                this.append(name, init[name]);
            });
        }
    }

    append(name, value) {
        name = name.toLowerCase();
        if (!this.headers[name]) {
            this.headers[name] = [];
        }
        this.headers[name].push(value);
    }

    delete(name) {
        name = name.toLowerCase();
        delete this.headers[name];
    }


    forEach(callback) {
        for (const name in this.headers) {
            if (this.headers.hasOwnProperty(name)) {
                const values = this.headers[name];
                name.split(',').forEach(callback.bind(null, values, name));
            }
        }
    }

    get(name) {
        name = name.toLowerCase();
        return this.headers[name] ? this.headers[name][0] : null;
    }

    has(name) {
        name = name.toLowerCase();
        return !!this.headers[name];
    }

    getSetCookie() {
        const setCookieHeaders = this.getAll('Set-Cookie');
        return setCookieHeaders.map(header => header.split(';')[0]);
    }

    values() {
        const result = [];
        this.forEach(value => {
            result.push(value);
        });
        return result;
    }

    getAll(name) {
        name = name.toLowerCase();
        return this.headers[name] || [];
    }

}

module.exports.fetch = restrictedFetch