import React from "react"
import { Link } from "react-router-dom"


export default function Log() {

    return(
        <div className="Log" >
            {/* <Link to="Login" > */}
                {/* <button className="btn btn-syccess dropdown-toggle" type="button" data-bs-toggle="dropdown" aria-expanded="false" >
                    Login
                </button> */}
            <div className="dropdown-menu dropdown-menu-dark" >
                <form class="px-3 py-3">
                    <div className="mb-3" >
                        <label className="form-label">Email address</label>
                        <input type="text" className="form-control" placeholder="Username123" />
                    </div>
                    <div className="mb-3" >
                        <label className="form-label">Password</label>
                        <input type="password" className="form-control" placeholder="Password123" />
                    </div>
                    <button type="submit" class="btn btn-primary" >Sign in</button>
                </form>
            </div>    


            {/* </Link> */}
            {/* |
            <Link to="Register" >
                Register
            </Link> */}
        </div>
    )
}