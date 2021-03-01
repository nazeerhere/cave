import React from "react"

export default function Register() {
    return(
        <div className="Register">
            <form>
                <small>
                    All User information will be kept private
                </small>
                {"\n"}
                <div>
                    <label>First name here</label>
                    <input type="text"
                            id="firstname"
                            placeholder="First name here"
                            // value=
                            // onChange=
                    />

                    <label>Last name here</label>
                    <input type="text"
                            id="firstname"
                            placeholder="First name here"
                            // value=
                            // onChange=
                    />

                    <label>Username</label>
                    <input type="text"
                            id="firstname"
                            placeholder="First name here"
                            // value={useState}
                            // onChange=
                    />

                    <label>Password</label>
                    <input type="text"
                            id="firstname"
                            placeholder="First name here"
                            // value={useState}
                            // onChange=
                    />
                </div>
                <button type="submit"
                        // onClick=
                >
                    Complete Regisration
                </button>
            </form>
        </div>
    )
}