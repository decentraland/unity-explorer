  
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

    let response = await UnitySimpleFetchApi.Fetch(
        reqMethod, url, reqHeaders, hasBody, body ?? '', reqRedirect, reqTimeout
    )
    
    response = { ...response };

    response.headers = new Headers(response.headers);

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
            return response.data
        }
    })

    return response
}

class Headers {
    constructor(init = {}) {
        this.headers = {};

        if (init instanceof Headers) {
            init.forEach((key, value) => {
                this.append(key, value);
            });
        } else if (Array.isArray(init)) {
            init.forEach(([key, value]) => {
                this.append(key, value);
            });
        } else if (init && typeof init === 'object') {
            Object.keys(init).forEach(key => {
                this.append(key, init[key]);
            });
        }
    }

    append(key, value) {
        if (!this.headers[key]) {
            this.headers[key] = [];
        }
        this.headers[key].push(value);
    }

    delete(key) {
        delete this.headers[key];
    }


    forEach(callback) {
        for (const key in this.headers) {
            if (this.headers.hasOwnProperty(key)) {
                const values = this.headers[key];
                key.split(',').forEach(callback.bind(null, values, key));
            }
        }
    }

    keys() {
        return Object.keys(this.headers);
    }


    set(key, value) {
        this.headers[key] = [value];
    }


    get(key) {
        return this.headers[key] ? this.headers[key][0] : null;
    }

    has(key) {
        return !!this.headers[key];
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

    getAll(key) {
        return this.headers[key] || [];
    }

}

module.exports.fetch = restrictedFetch