import React from "react"
import { Link } from "react-router-dom"


export default function Login() {
    return(
        <div className="Login" >
            <div className="space" >
            <span><h3>Login Here:</h3></span>
            <form>
                <label>Username:</label>
                <input type="text"
                        id="username"
                        placeholder="Username"
                        // value=()
                        // onChange=
                />

                <label>Password:</label>
                <input type="text"
                        id="password"
                        placeholder="password"
                        // value=()
                        // onChange=
                />

                <button
                    type="submit"
                    // onClick=
                >
                    <Link to="/" >Login</Link>
                </button>
                Not a member? <Link to="/register" className="text-warning">
                        Register here
                    </Link>
            </form>
        </div>
    </div>
    )
}